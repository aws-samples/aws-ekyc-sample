using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.THAI_ID;

public class Thai_ID_Back_DocumentDefinition : DocumentDefinitionBase
{
    private readonly ILogger<Thai_ID_Back_DocumentDefinition> _logger;
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonS3 _s3;
    private readonly IAmazonTextract _textract;

    public Thai_ID_Back_DocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
        ILogger<Thai_ID_Back_DocumentDefinition> logger, IAmazonTextract textract) : base(
        config, rekognition, s3, textract)
    {
        _logger = logger;
        _textract = textract;
        _rekognition = rekognition;
        _s3 = s3;
    }

    public override bool LivenessSupported { get; set; }

    public override string Name
    {
        get => "Thai ID back";
        set { }
    }

    public override NamedBoundingBox[] Landmarks { get; set; }
    public override NamedBoundingBox[] DataFields { get; set; }
    public override string RekognitionCustomLabelsProjectArn { get; set; }

    public override DocumentTypes DocumentType
    {
        get => DocumentTypes.THAI_ID_BACK;
        set { }
    }

    public override bool FaceExtractionSupported
    {
        get => false;
        set { }
    }

    public override bool SignatureExtractionSupported
    {
        get => false;
        set { }
    }

    public override async Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("Thai_ID_Back_DocumentDefinition::GetFieldData");

        var detectResponse = await _textract.DetectDocumentTextAsync(new DetectDocumentTextRequest
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

        var values = new Dictionary<string, string>();

        for (var i = 0; i < detectResponse.Blocks.Count; i++)
            if (detectResponse.Blocks[i].BlockType == BlockType.LINE &&
                !string.IsNullOrEmpty(detectResponse.Blocks[i].Text))
                values.Add($"Field {i}", detectResponse.Blocks[i].Text);

        AWSXRayRecorder.Instance.EndSubsegment();
        return values;
    }
}