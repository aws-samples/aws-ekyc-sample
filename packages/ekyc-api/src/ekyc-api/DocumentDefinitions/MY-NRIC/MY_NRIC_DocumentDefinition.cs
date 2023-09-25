using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ekyc_api.DocumentDefinitions;

public class MY_NRIC_DocumentDefinition : DocumentDefinitionBase
{
    private readonly ILogger<MY_NRIC_DocumentDefinition> _logger;
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonTextract _textract;

    public MY_NRIC_DocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
        ILogger<MY_NRIC_DocumentDefinition> logger, IAmazonTextract textract) : base(
        config, rekognition, s3, textract)
    {
        _logger = logger;
        _textract = textract;
        _rekognition = rekognition;
    }

    public override bool SignatureExtractionSupported
    {
        get => false;
        set { }
    }

    public override bool FaceExtractionSupported
    {
        get => true;
        set { }
    }

    public override bool LivenessSupported
    {
        get => true;
        set { }
    }


    public override string Name
    {
        get => "Malaysian NRIC";
        set { }
    }

    public override DocumentTypes DocumentType
    {
        get => DocumentTypes.MY_NRIC;
        set { }
    }

    public override NamedBoundingBox[] Landmarks
    {
        get
        {
            var lstLandmarks = new List<NamedBoundingBox>();

            lstLandmarks.Add(new NamedBoundingBox
            {
                Name = "MyKadLogo",
                ExpectedBoundingBox = new BoundingBox
                {
                    Left = 0.57f,
                    Width = 0.15f,
                    Top = 0.02f,
                    Height = 0.21f
                }
            });

            return lstLandmarks.ToArray();
        }
        set { }
    }

    public override NamedBoundingBox[] DataFields { get; set; }

    public override string RekognitionCustomLabelsProjectArn { get; set; }

    public override async Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType)
    {
        var getObjectResponse = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = Globals.StorageBucket,
            Key = S3Key
        });

        var imageData = getObjectResponse.ResponseStream;

        // Increase saturation and make the image B&W - this improves accuracy

        var processedS3Key = await PerformPreprocessing(S3Key, imageData);

        try
        {
            //var fieldExtractor = new MYKad_RekognitionFieldValueExtractor(_rekognition, _config, _logger);
            var fieldExtractor = new MYKad_TextractFieldValueExtractor(_textract, _config, _logger);


            // then process the full image
            var values =
                await fieldExtractor.GetFieldValues(processedS3Key, RekognitionCustomLabelsProjectArn, DocumentType);

            await S3Utils.DeleteFromS3(processedS3Key);

            if (values.Count == 0)
                return await base.GetFieldData(S3Key, docType);
            return values;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"An error occurred with the field extractor - {ex.Message}. This is not fatal. Trying the Textract version.");
            return await base.GetFieldData(S3Key, docType);
        }

        //return new Dictionary<string, string>();
    }


    private async Task<string> PerformPreprocessing(string s3Key, Stream imageData)
    {
        var img = Image.Load(imageData);

        // img.Mutate(i => i.Contrast(5f).Grayscale().Contrast(1.2f));

        img.Mutate(i => i.Grayscale().Contrast(1.2f));

        img.Mutate(i => i.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1000) }));

        var ms = new MemoryStream();

        img.SaveAsJpeg(ms);

#if DEBUG
        if (Environment.GetEnvironmentVariable("Environment") == "Unit Test")
            img.Save($"../../{DateTime.Now.Ticks.ToString()}.jpg");

#endif

        ms.Seek(0, SeekOrigin.Begin);

        var newKey = "temp/" + Guid.NewGuid() + ".jpg";

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Globals.StorageBucket,
            Key = newKey,
            InputStream = ms
        });

        return newKey;
    }
}