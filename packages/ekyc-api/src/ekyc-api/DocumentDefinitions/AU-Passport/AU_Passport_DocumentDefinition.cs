using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using ekyc_api.DataDefinitions;
using Microsoft.Extensions.Configuration;

namespace ekyc_api.DocumentDefinitions.AU_Passport
{
    public class AU_Passport_DocumentDefinition : DocumentDefinitionBase
    {
        public AU_Passport_DocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
            IAmazonTextract textract) :
            base(config, rekognition, s3, textract)
        {
        }


        public override bool LivenessSupported { get; set; }
        public override bool FaceExtractionSupported { get; set; }
        public override bool SignatureExtractionSupported { get; set; }

        public override string Name
        {
            get => "Australian Passport";
            set { }
        }


        public override DocumentTypes DocumentType
        {
            get { return DocumentTypes.AU_PASSPORT; }
            set { }
        }

        public override string RekognitionCustomLabelsProjectArn { get; set; }

        public override NamedBoundingBox[] Landmarks { get; set; }
        public override NamedBoundingBox[] DataFields { get; set; }
    }
}