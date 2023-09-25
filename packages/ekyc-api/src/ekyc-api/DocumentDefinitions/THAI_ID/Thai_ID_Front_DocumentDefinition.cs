using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions.CN_Passport;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ekyc_api.DocumentDefinitions.THAI_ID;

public class Thai_ID_Front_DocumentDefinition : DocumentDefinitionBase
{
    private readonly IAmazonS3 _amazonS3;
    private readonly IConfiguration _config;
    private readonly ILogger<PRC_Passport_DocumentDefinition> _logger;
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonTextract _textract;

    public Thai_ID_Front_DocumentDefinition(IConfiguration config,
        IAmazonRekognition rekognition, IAmazonS3 s3,
        ILogger<Thai_ID_Front_DocumentDefinition> logger,
        IAmazonTextract textract) :
        base(config, rekognition, s3, textract)
    {
        _config = config;
        _rekognition = rekognition;
        _amazonS3 = s3;
        _textract = textract;
    }

    public override bool LivenessSupported
    {
        get => true;
        set { }
    }

    public override string Name
    {
        get => "Thai ID front";
        set { }
    }

    public override NamedBoundingBox[] Landmarks { get; set; }
    public override NamedBoundingBox[] DataFields { get; set; }

    public override string RekognitionCustomLabelsProjectArn
    {
        get =>
            "arn:aws:rekognition:ap-southeast-1:886995061454:project/thai-id-1/version/thai-id-1.2023-09-04T00.16.51/1693750611696";
        set { }
    }

    public override DocumentTypes DocumentType
    {
        get => DocumentTypes.THAI_ID_FRONT;
        set { }
    }

    public override bool FaceExtractionSupported
    {
        get => true;
        set { }
    }

    public override bool SignatureExtractionSupported
    {
        get => false;
        set { }
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

    public override async Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType)
    {
        var extractor = new Thai_ID_Front_TextractFieldValueExtractor(_s3, _textract, _config, _logger);

        return await extractor.GetFieldValues(S3Key, RekognitionCustomLabelsProjectArn, docType);
    }
}