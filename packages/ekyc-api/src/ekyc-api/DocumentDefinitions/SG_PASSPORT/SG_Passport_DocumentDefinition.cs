using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions.CN_Passport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.SG_PASSPORT
{
    public class SG_Passport_DocumentDefinition : DocumentDefinitionBase
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IConfiguration _config;
        private readonly ILogger<PRC_Passport_DocumentDefinition> _logger;
        private readonly IAmazonRekognition _rekognition;
        private readonly IAmazonTextract _textract;

        public SG_Passport_DocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
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
            get { return true; }
            set { }
        }

        public override string Name
        {
            get { return "Singapore Passport"; }
            set { }
        }

        public override NamedBoundingBox[] Landmarks { get; set; }
        public override NamedBoundingBox[] DataFields { get; set; }
        public override string RekognitionCustomLabelsProjectArn { get; set; }

        public override DocumentTypes DocumentType
        {
            get { return DocumentTypes.SG_PASSPORT; }
            set { }
        }

        public override bool FaceExtractionSupported { get; set; }
        public override bool SignatureExtractionSupported { get; set; }
    }
}