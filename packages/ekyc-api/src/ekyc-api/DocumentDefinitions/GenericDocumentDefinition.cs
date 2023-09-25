using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions;

public class GenericDocumentDefinition : DocumentDefinitionBase
{
    private readonly ILogger<GenericDocumentDefinition> _logger;
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonTextract _textract;

    public GenericDocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
        ILogger<GenericDocumentDefinition> logger, IAmazonTextract textract) : base(
        config, rekognition, s3, textract)
    {
        _logger = logger;
        _textract = textract;
        _rekognition = rekognition;
    }

    public override bool LivenessSupported
    {
        get => false;
        set { }
    }

    public override string Name
    {
        get => "Generic Document";
        set { }
    }

    public override NamedBoundingBox[] Landmarks { get; set; }

    public override NamedBoundingBox[] DataFields { get; set; }
    public override string RekognitionCustomLabelsProjectArn { get; set; }

    public override DocumentTypes DocumentType
    {
        get => DocumentTypes.GENERIC;
        set { }
    }

    public override bool FaceExtractionSupported
    {
        get => true;
        set { }
    }

    public override bool SignatureExtractionSupported
    {
        get => true;
        set { }
    }
}