using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.CN_Passport
{
    public class PRC_Passport_DocumentDefinition : DocumentDefinitionBase
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IConfiguration _config;
        private readonly ILogger<PRC_Passport_DocumentDefinition> _logger;
        private readonly IAmazonRekognition _rekognition;
        private readonly IAmazonTextract _textract;

        public PRC_Passport_DocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
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

        public override string Name
        {
            get => "PRC Passport";
            set { }
        }

        public override DocumentTypes DocumentType
        {
            get { return DocumentTypes.PRC_PASSPORT; }
            set { }
        }

        public override NamedBoundingBox[] Landmarks { get; set; }
        public override NamedBoundingBox[] DataFields { get; set; }
        public override string RekognitionCustomLabelsProjectArn { get; set; }

        public override async Task<Dictionary<string, string>> PostProcessFieldData(Dictionary<string, string> Values)
        {
            var returnValues = new Dictionary<string, string>();

            foreach (var k in Values.Keys)
            {
                var strValue = Values[k];

                if (strValue.StartsWith("/"))
                    strValue = strValue.Substring(1).Trim();

                returnValues[k] = strValue;
            }

            return returnValues;
        }

        public override async Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType)
        {
            var values = await base.GetFieldData(S3Key, docType);

            if (values.Count == 0)
            {
                var extractor = new PRC_Passport_TextractFieldValueExtractor(_textract, _config, _logger);

                values = await extractor.GetFieldValues(S3Key);
                return values;
            }
            else
            {
                return values;
            }
        }
    }
}