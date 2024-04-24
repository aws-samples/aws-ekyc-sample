using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Transform;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using S3Object = Amazon.Textract.Model.S3Object;

namespace ekyc_api.DocumentDefinitions.THAI_ID;

public class Thai_ID_Front_TextractFieldValueExtractor : IFieldValueExtractor
{
    private readonly IConfiguration _config;
    private readonly IDocumentDefinitionFactory _factory;
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonTextract _textractClient;

    private readonly string[] ThaiNamePrefixes =
    {
        "น.ส.", "นาง", "นาย", "ด.ญ.", "ด.ช.", "พระสงฆ์", "บาทหลวง", "หม่อมหลวง", "หม่อมราชวงศ์", "หม่อมเจ้า",
        "ศาสตราจารย์เกียรติคุณ (กิตติคุณ)", "ศาสตราจารย์", "ผู้ช่วยศาสตราจารย์", "รองศาสตราจารย์"
    };


    public Thai_ID_Front_TextractFieldValueExtractor(IAmazonS3 s3Client, IAmazonTextract textractClient,
        IDocumentDefinitionFactory factory,
        IConfiguration config,
        ILogger logger)
    {
        _config = config;
        _factory = factory;
        _textractClient = textractClient;
        _s3Client = s3Client;
    }

    public async Task<Dictionary<string, string>> GetFieldValues(string s3Key, string RekognitionCustomLabelsProjectArn,
        DocumentTypes documentType)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("Thai_ID_Front_TextractFieldValueExtractor::GetFieldValues");

        var newKey = await Imaging.CropDocumentByLabel(s3Key, "Driving License");

        var values = new Dictionary<string, string>();

        // if (RekognitionCustomLabelsProjectArn != null)
        //     newKey = await Imaging.CropDocument(s3Key, RekognitionCustomLabelsProjectArn, documentType);

        // Find the identification bounding box from template and actual

        // Step 1. Use bounding box model to extract ID

        var getObjectResponse = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = Globals.StorageBucket,
            Key = newKey
        });

        var msImage = new MemoryStream();

        await getObjectResponse.ResponseStream.CopyToAsync(msImage);
        msImage.Seek(0, SeekOrigin.Begin);
        var originalImage = await Image.LoadAsync(msImage);

        var newWidth = 1000;
        var newHeight = (int)(newWidth * ((double)originalImage.Height / originalImage.Width));

        // Make the image greyscale and increase contrast to make it easier for Textract
        originalImage.Mutate(i => i.Resize(newWidth, newHeight));
        //     .GaussianSharpen(0.5f).Contrast(1));


        // Step 2. Use Textract to obtain bounding boxes of known “anchor” points

        // var analyzeIdResponse = await AnalyzeID(newKey);
        //
        // Console.WriteLine($"Analyze ID from image: {JsonSerializer.Serialize(analyzeIdResponse)}");
        //
        // foreach (var detectedValue in analyzeIdResponse.Where(a =>
        //              a.ValueDetection.Confidence > 50 && !string.IsNullOrEmpty(a.ValueDetection.Text)))
        //     values.Add(detectedValue.Type.Text, detectedValue.ValueDetection.Text);

        var strThaiDateRegex = @"^\d{2}\s[A-Z]{1}[a-z]{2}\.\s\d{4}$";
        var thaiDateRegex = new Regex(strThaiDateRegex);

        var textBlocks = await GetTextractValuesFromImage(msImage);

        msImage.Seek(0, SeekOrigin.Begin);

        var idBlock = textBlocks.FirstOrDefault(tb => tb.Text?.Trim() == "Identification Number");

        if (idBlock == null) throw new Exception("Cannot find identification number text block on ID.");

        var nameBlock =
            textBlocks.FirstOrDefault(tb =>
                tb.BlockType == BlockType.LINE && tb.Text != null && tb.Text.Trim().StartsWith("Name"));

        if (nameBlock == null) throw new Exception("Cannot find name text block on ID.");

        var idTitleBlock = textBlocks.FirstOrDefault(tb => tb.Text?.Trim().StartsWith("Thai National ID") ?? false);

        if (idTitleBlock == null) throw new Exception("Cannot find identification number title text block on ID.");

        var dobBlock = textBlocks.FirstOrDefault(tb => tb.Text != null && tb.Text.StartsWith("Date of Birth"));

        if (dobBlock != null) values.Add("Date of Birth", dobBlock.Text.Substring("Date of Birth".Length + 1));

        Console.WriteLine(JsonSerializer.Serialize(dobBlock.Geometry));

        // Flag to state if blocks need to be retrieved again - this is needed if a rotation has been done.
        var needsBlockRetrieval = false;

        if (dobBlock.Geometry.Polygon[0].Y < dobBlock.Geometry.Polygon[1].Y)
        {
            // Card needs to be rotated anticlockwise
            // Opposite / adjacent
            var degreesToRotate = Math.Atan((dobBlock.Geometry.Polygon[1].Y - dobBlock.Geometry.Polygon[0].Y) /
                                            (dobBlock.Geometry.Polygon[1].X - dobBlock.Geometry.Polygon[0].X));

            degreesToRotate = Imaging.ConvertRadiansToDegrees(degreesToRotate);
            originalImage.Mutate(i => i.Rotate(Convert.ToSingle(degreesToRotate * -1d)));
            needsBlockRetrieval = true;
        }
        else if (dobBlock.Geometry.Polygon[0].Y > dobBlock.Geometry.Polygon[1].Y)
        {
            // Card needs to be rotated clockwise

            // Opposite / adjacent
            var degreesToRotate = Math.Atan((dobBlock.Geometry.Polygon[0].Y - dobBlock.Geometry.Polygon[1].Y) /
                                            (dobBlock.Geometry.Polygon[1].X - dobBlock.Geometry.Polygon[0].X));
            degreesToRotate = Imaging.ConvertRadiansToDegrees(degreesToRotate);

            originalImage.Mutate(i => i.Rotate(Convert.ToSingle(degreesToRotate)));
            needsBlockRetrieval = true;
        }

        if (!Globals.IsRunningOnLambda) await originalImage.SaveAsPngAsync("rotated.png");

        // using (var ms = new MemoryStream())
        // {
        //     await originalImage.SaveAsPngAsync(ms);
        //     var fieldValues = await Tesseract.GetThaiIdFrontDataFromTesseract(ms, "front.png");
        //
        //     var items = Globals.ToDictionary<string>(fieldValues);
        //     foreach (var item in items)
        //         values.Add(item.Key, item.Value);
        // }

        if (needsBlockRetrieval)
            using (var ms = new MemoryStream())
            {
                await originalImage.SaveAsPngAsync(ms);
                textBlocks = await GetTextractValuesFromImage(ms);
            }


        var doeBlock = textBlocks.FirstOrDefault(tb => tb.Text?.Trim() == "Date of Expiry");

        if (doeBlock == null) throw new Exception("Cannot find date of expiry text block on ID.");

        var doiBlock = textBlocks.FirstOrDefault(tb => tb.Text?.Trim() == "Date of Issue");

        if (doiBlock == null) throw new Exception("Cannot find date of issue text block on ID.");

        using (var ms = new MemoryStream())
        {
            await originalImage.SaveAsPngAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var tessResponse = await Tesseract.GetThaiIdFrontDataFromTesseract(ms, "front.png");
            values = values
                .Union(tessResponse.ToDictionary())
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        // Get fields from base

        // var fieldData = await GetFieldDataByCoordinates(originalImage, textBlocks);
        //
        // if (fieldData.Count > 0)
        //     values = values
        //         .Union(fieldData)
        //         .ToDictionary(pair => pair.Key, pair => pair.Value);

        // Find the date of expiry

        var doeValueBlock = textBlocks
            .Where(a => a.BlockType == BlockType.LINE)
            .Where(a => thaiDateRegex.IsMatch(a.Text.Trim()))
            // The left edge must be within 5% of the date of expiry title block
            .Where(a => Math.Abs(a.Geometry.BoundingBox.Left - doeBlock.Geometry.BoundingBox.Left) < 0.05)
            .OrderByDescending(a => a.Geometry.BoundingBox.Top)
            .ThenBy(a => a.Geometry.BoundingBox.Left)
            .FirstOrDefault();

        if (doeValueBlock != null && !string.IsNullOrEmpty(doeValueBlock.Text))
            values["Date of Expiry"] = doeValueBlock.Text;

        // Find the date of issue
        var doiValueBlock = textBlocks
            .Where(a => a.BlockType == BlockType.LINE)
            .Where(a => thaiDateRegex.IsMatch(a.Text.Trim()))
            // The left edge must be within 5% of the date of issue title block
            .Where(a => Math.Abs(a.Geometry.BoundingBox.Left - doiBlock.Geometry.BoundingBox.Left) < 0.05)
            .OrderByDescending(a => a.Geometry.BoundingBox.Top)
            .ThenBy(a => a.Geometry.BoundingBox.Left)
            .FirstOrDefault();

        if (doiValueBlock != null && !string.IsNullOrEmpty(doiValueBlock.Text))
            values["Date of Issue"] = doiValueBlock.Text;


        // Thai name - we need to use the left edge of date of issue
        var thaiNameImage =
            await Imaging.CloneCropImage(originalImage,
                idBlock.Geometry.BoundingBox.Top + idBlock.Geometry.BoundingBox.Height,
                nameBlock.Geometry.BoundingBox.Left - 0.05f,
                1 - nameBlock.Geometry.BoundingBox.Left + 0.02f,
                nameBlock.Geometry.BoundingBox.Top -
                (idBlock.Geometry.BoundingBox.Top + idBlock.Geometry.BoundingBox.Height));

        using (var ms = new MemoryStream())
        {
            await thaiNameImage.SaveAsPngAsync(ms);
            if (!Globals.IsRunningOnLambda) await thaiNameImage.SaveAsPngAsync("thai-name.png");
            ms.Seek(0, SeekOrigin.Begin);

            var tessResponse = await Tesseract.CallTesseractApi(ms, "thai-name.png");
            if (!string.IsNullOrEmpty(tessResponse.result))
                values["FullNameTH"] = tessResponse.result;
        }

        var lastNamePrefix = "Last name ";
        var lastNameBlock =
            textBlocks.FirstOrDefault(tb =>
                tb.Text?.StartsWith(lastNamePrefix) == true && tb.BlockType == BlockType.LINE);
        if (lastNameBlock != null)
            if (lastNameBlock.Text.Length > lastNamePrefix.Length)
                values["LastNameEN"] = lastNameBlock.Text.Substring(lastNamePrefix.Length);

        // Thai Address - we need to use the left edge of date of issue
        var thaiAddressImage =
            await Imaging.CloneCropImage(originalImage,
                doiBlock.Geometry.BoundingBox.Top - 0.3f,
                doiBlock.Geometry.BoundingBox.Left,
                0.75f - doiBlock.Geometry.BoundingBox.Left,
                doiBlock.Geometry.BoundingBox.Height * 5);

        using (var ms = new MemoryStream())
        {
            await thaiAddressImage.SaveAsPngAsync(ms);
            if (!Globals.IsRunningOnLambda) await thaiAddressImage.SaveAsPngAsync("thai-address.png");
            ms.Seek(0, SeekOrigin.Begin);

            var tessResponse = await Tesseract.CallTesseractApi(ms, "thai-address.png");
            if (!string.IsNullOrEmpty(tessResponse.result))
                values.Add("Thai Address", tessResponse.result);
        }

        if (values.ContainsKey("FullNameTH"))
        {
            var gender = GenderPrefixMatcher(values["FullNameTH"]);
            if (!string.IsNullOrEmpty(gender))
                values.Add("Gender", gender);
        }

        AWSXRayRecorder.Instance.EndSubsegment();

        return values;
    }

    public async Task<Dictionary<string, string>> GetFieldDataByCoordinates(Image image, List<Block> Blocks)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("DocumentDefinition::GetFieldDataByCoordinates");

        var returnVal = new Dictionary<string, string>();

        var definition = await _factory.GetDocumentDefinition(DocumentTypes.THAI_ID_FRONT);

        if (definition.DataFields == null || definition.DataFields.Length == 0)
            return returnVal;


        var lines = Blocks
            .Where(x => x.Confidence > Globals.GetMinimumConfidence() && x.BlockType == BlockType.LINE)
            .OrderBy(x => x.Geometry.BoundingBox.Top)
            .ThenBy(x => x.Geometry.BoundingBox.Left)
            .ToList();


        // foreach (var line in lines)
        //    Console.WriteLine(line.Text);
        //      $"{line.Text} - X: {line.Geometry.BoundingBox.Left} Y: {line.Geometry.BoundingBox.Top} Width: {line.Geometry.BoundingBox.Width} Height: {line.Geometry.BoundingBox.Height}");


        foreach (var field in definition.DataFields)
            if (field.Language == null || field.Language.ToLower() != "th")
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
            else
            {
                // Use tesseract
                var croppedImage = await Imaging.CloneCropImage(image,
                    Convert.ToSingle(field.ExpectedBoundingBox.Left),
                    Convert.ToSingle(field.ExpectedBoundingBox.Top),
                    Convert.ToSingle(field.ExpectedBoundingBox.Width),
                    Convert.ToSingle(field.ExpectedBoundingBox.Height));

                if (!Globals.IsRunningOnLambda) await croppedImage.SaveAsPngAsync($"thai-{field.Name}.png");
                using (var ms = new MemoryStream())
                {
                    await croppedImage.SaveAsPngAsync(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    var response = await Tesseract.CallTesseractApi(ms, "field.png");
                    if (!string.IsNullOrEmpty(response.result))
                        returnVal.Add(field.Name, response.result);
                }
            }

        AWSXRayRecorder.Instance.EndSubsegment();

        return returnVal;
    }

    private string GenderPrefixMatcher(string thaiName)
    {
        if (thaiName.StartsWith("ด.ญ.") || thaiName.StartsWith("นาง") || thaiName.StartsWith("น.ส.") ||
            thaiName.StartsWith(".น.ส."))
            return "Female";
        if (thaiName.StartsWith("นาย") || thaiName.StartsWith("ด.ช.") || thaiName.StartsWith("พระสงฆ์") ||
            thaiName.StartsWith("บาทหลวง"))
            return "Male";
        return "Unknown";
    }

    private async Task<List<Block>> GetTextractValuesFromImage(MemoryStream ms)
    {
        var response = _textractClient.DetectDocumentTextAsync(new DetectDocumentTextRequest
        {
            Document = new Document
            {
                Bytes = ms
            }
        });
        return response.Result.Blocks;
    }

    private async Task<List<IdentityDocumentField>> AnalyzeID(string S3Key)
    {
        var response = await _textractClient.AnalyzeIDAsync(new AnalyzeIDRequest
        {
            DocumentPages = new List<Document>
            {
                new()
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = S3Key
                    }
                }
            }
        });

        return response.IdentityDocuments.FirstOrDefault()?.IdentityDocumentFields ?? new List<IdentityDocumentField>();
    }
}