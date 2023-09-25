using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Image = Amazon.Rekognition.Model.Image;
using S3Object = Amazon.Textract.Model.S3Object;

namespace ekyc_api.DocumentDefinitions;

public abstract class DocumentDefinitionBase : IDocumentDefinition
{
    protected readonly IConfiguration _config;
    protected readonly ILogger _logger;
    protected readonly IAmazonRekognition _rekognition;
    protected readonly IAmazonS3 _s3;
    protected readonly IAmazonTextract _textract;

    public DocumentDefinitionBase(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
        IAmazonTextract textract)
    {
        _config = config;
        _rekognition = rekognition;
        _s3 = s3;
        _textract = textract;
        _logger = ServiceActivator._serviceProvider.GetService(typeof(ILogger)) as ILogger;
    }

    public DocumentDefinitionBase()
    {
        // We need a default constructor for reflection
    }

    public abstract DocumentTypes DocumentType { get; set; }


    public abstract bool LivenessSupported { get; set; }

    public abstract bool FaceExtractionSupported { get; set; }

    public abstract bool SignatureExtractionSupported { get; set; }

    public abstract string Name { get; set; }

    public virtual async Task<Dictionary<string, string>> PostProcessFieldData(Dictionary<string, string> Values)
    {
        return Values;
    }


    public virtual async Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("DocumentDefinition::GetFieldData");

        if (Globals.UseFieldCoordinatesExtractionMethod)
            return await GetFieldDataByCoordinates(S3Key);

        // Use the new Textract AnalyzeID API

        var documentPages = new List<Document>();

        documentPages.Add(new Document { S3Object = new S3Object { Bucket = Globals.StorageBucket, Name = S3Key } });

        var response = await _textract.AnalyzeIDAsync(new AnalyzeIDRequest { DocumentPages = documentPages });


        var returnVal = new Dictionary<string, string>();

        if (response!.IdentityDocuments!.Count == 0)
            return new Dictionary<string, string>();

        var lstResults = response.IdentityDocuments[0].IdentityDocumentFields
            .Where(a => a.ValueDetection.Confidence >= Globals.GetMinimumConfidence() &&
                        !string.IsNullOrEmpty(a.ValueDetection!.Text))
            .OrderByDescending(a => a.Type.Text).ToList();

        foreach (var result in lstResults) returnVal[result.Type.Text] = result.ValueDetection.Text;

        AWSXRayRecorder.Instance.EndSubsegment();

        return returnVal;
    }

    public async Task<MemoryStream> GetFace(string S3Key)
    {
        if (!FaceExtractionSupported)
            return null;

        var response = await _rekognition.DetectFacesAsync(new DetectFacesRequest
        {
            Image = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = Globals.StorageBucket,
                    Name = S3Key
                }
            },
            Attributes = new List<string>(new[] { "ALL" })
        });

        var faceDetails = response.FaceDetails.Where(f => f.Confidence >= Globals.GetMinimumConfidence())
            .ToList();

        if (faceDetails.Count == 0)
            return null;

        var biggestFace = response.FaceDetails
            .Where(f => f.Confidence >= Globals.GetMinimumConfidence())
            .OrderByDescending(
                f => f.BoundingBox.Width + f.BoundingBox.Height
            ).FirstOrDefault();

        if (biggestFace == null)
            return null;

        // Cut the face image out from original image

        var getObjectResponse = await _s3.GetObjectAsync(Globals.StorageBucket, S3Key);

        var originalImage = SixLabors.ImageSharp.Image.Load(getObjectResponse.ResponseStream);

        var x = Convert.ToInt32(biggestFace.BoundingBox.Left * Convert.ToSingle(originalImage.Width));
        var y = Convert.ToInt32(biggestFace.BoundingBox.Top * Convert.ToSingle(originalImage.Height));
        var width = Convert.ToInt32(biggestFace.BoundingBox.Width * Convert.ToSingle(originalImage.Width));
        var height = Convert.ToInt32(biggestFace.BoundingBox.Height * Convert.ToSingle(originalImage.Height));

        var faceImage = originalImage.Clone(i => i.Crop(new Rectangle(x, y, width, height)));

        var ms = new MemoryStream();

        faceImage.SaveAsJpeg(ms);

        return ms;
    }

    public abstract NamedBoundingBox[] Landmarks { get; set; }

    public abstract NamedBoundingBox[] DataFields { get; set; }

    public abstract string RekognitionCustomLabelsProjectArn { get; set; }


    public async Task<Dictionary<string, string>> GetFieldDataByCoordinates(string S3Key)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("DocumentDefinition::GetFieldDataByCoordinates");

        var returnVal = new Dictionary<string, string>();

        if (DataFields == null)
            return returnVal;

        var response = await _textract.DetectDocumentTextAsync(new DetectDocumentTextRequest
        {
            Document = new Document
            {
                S3Object = new S3Object
                {
                    Bucket = Globals.StorageBucket,
                    Name = S3Key
                }
            }
        });

        var lines = response.Blocks
            .Where(x => x.Confidence > Globals.GetMinimumConfidence() && x.BlockType == BlockType.LINE)
            .OrderBy(x => x.Geometry.BoundingBox.Top)
            .ThenBy(x => x.Geometry.BoundingBox.Left)
            .ToList();


        // foreach (var line in lines)
        //    Console.WriteLine(line.Text);
        //      $"{line.Text} - X: {line.Geometry.BoundingBox.Left} Y: {line.Geometry.BoundingBox.Top} Width: {line.Geometry.BoundingBox.Width} Height: {line.Geometry.BoundingBox.Height}");


        foreach (var field in DataFields)
        {
            // Get all the extracted lines that fall within the expected bounding box of the field
            var matchingLines = lines.Where(f =>
                    Math.Abs(f.Geometry.BoundingBox.Left - field.ExpectedBoundingBox.Left) <=
                    Globals.BoundingBoxVarianceThreshold &&
                    Math.Abs(f.Geometry.BoundingBox.Top - field.ExpectedBoundingBox.Top) <=
                    Globals.BoundingBoxVarianceThreshold &&
                    field.ExpectedBoundingBox.Width + Globals.BoundingBoxVarianceThreshold >=
                    f.Geometry.BoundingBox.Width &&
                    field.ExpectedBoundingBox.Height + Globals.BoundingBoxVarianceThreshold >=
                    f.Geometry.BoundingBox.Height
                ).OrderBy(f => f.Geometry.BoundingBox.Top)
                .ThenBy(f => f.Geometry.BoundingBox.Left)
                .Select(f => f.Text)
                .ToArray();

            if (matchingLines.Length > 0)
            {
                if (string.IsNullOrEmpty(field.RegexExpression))
                {
                    returnVal[field.Name] = string.Join(" ", matchingLines);
                }
                else
                {
                    var regex = new Regex(field.RegexExpression, RegexOptions.IgnoreCase);

                    var matchLine = matchingLines.FirstOrDefault(a => regex.IsMatch(a));

                    if (matchLine != null)
                        returnVal[field.Name] = matchLine;
                }
            }
        }

        returnVal = await PostProcessFieldData(returnVal);

        AWSXRayRecorder.Instance.EndSubsegment();

        return returnVal;
    }
}