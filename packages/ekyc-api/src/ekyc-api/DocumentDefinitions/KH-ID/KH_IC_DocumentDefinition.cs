using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.KH_ID;

public class KH_IC_DocumentDefinition : DocumentDefinitionBase
{
    private readonly IAmazonS3 _amazonS3;

    private readonly ILogger<KH_IC_DocumentDefinition> _logger;
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonTextract _textract;

    public KH_IC_DocumentDefinition(IConfiguration config, ILogger<KH_IC_DocumentDefinition> logger,
        IAmazonRekognition rekognition, IAmazonS3 s3, IAmazonTextract textract) : base(
        config, rekognition, s3, textract)
    {
        _amazonS3 = _s3;
        _logger = logger;
        _textract = textract;
        _rekognition = rekognition;
    }

    public override NamedBoundingBox[] DataFields { get; set; }
    public override string RekognitionCustomLabelsProjectArn { get; set; }

    public override NamedBoundingBox[] Landmarks
    {
        get { return new NamedBoundingBox[] { }; }
        set { }
    }

    public override DocumentTypes DocumentType
    {
        get => DocumentTypes.KH_IC;
        set { }
    }

    public override bool LivenessSupported
    {
        get => true;
        set { }
    }

    public override string Name
    {
        get => "Cambodian IC";
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

    public override async Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType)
    {
        var fieldExtractor = new KH_IC_TextractFieldValueExtractor(_textract, _config, _logger);

        var values = await fieldExtractor.GetFieldValues(S3Key, RekognitionCustomLabelsProjectArn,
            DocumentType);

        if (values.Count == 0)
            return await base.GetFieldData(S3Key, docType);
        return values;
    }
}