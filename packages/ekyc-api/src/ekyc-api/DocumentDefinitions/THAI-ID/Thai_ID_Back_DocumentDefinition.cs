using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.THAI_ID;

public class Thai_ID_Back_DocumentDefinition : DocumentDefinitionBase
{
    private readonly ILogger<Thai_ID_Back_DocumentDefinition> _logger;
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonTextract _textract;

    public Thai_ID_Back_DocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
        ILogger<Thai_ID_Back_DocumentDefinition> logger, IAmazonTextract textract) : base(
        config, rekognition, s3, textract)
    {
        _logger = logger;
        _textract = textract;
        _rekognition = rekognition;
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
}