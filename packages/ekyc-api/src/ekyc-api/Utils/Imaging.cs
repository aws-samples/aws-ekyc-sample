using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ekyc_api.Utils
{
    public class Imaging
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IDocumentChecker _documentChecker;


        public Imaging(IAmazonS3 amazonS3, IDocumentChecker documentChecker)
        {
            _amazonS3 = amazonS3;
            _documentChecker = documentChecker;
        }

        public async Task<Image> GetImageFromStorage(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("Utils::GetImageFromStorage");

            if (string.IsNullOrEmpty(s3Key))
            {
                var ex = new Exception("S3 key must be provided.");
                AWSXRayRecorder.Instance.AddException(ex);
                throw ex;
            }


            var response = await _amazonS3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = Globals.StorageBucket,
                Key = s3Key
            });

            var img = Image.Load(response.ResponseStream);

            AWSXRayRecorder.Instance.EndSubsegment();

            return img;
        }

        public async Task<(DocumentTypes documentType, MemoryStream CroppedImage, BoundingBox DocumentBoundingBox)?>
            DetectAndCropDocument(
                string sourceS3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("Utils::DetectAndCropDocument");

            var documentType = await _documentChecker.GetDocumentType(sourceS3Key);

            if (documentType == null || documentType.Type == null)
                return null;

            // Check to make sure document isn't smaller than the minimum size

            if (documentType.BoundingBox.Height < Globals.MinDocumentHeight ||
                documentType.BoundingBox.Width < Globals.MinDocumentWidth)
                throw new Exception("Document does not meet minimum size requirements.");

            // Crop the image

            var getResponse = await _amazonS3.GetObjectAsync(new GetObjectRequest
                { BucketName = Globals.StorageBucket, Key = sourceS3Key });

            var img = Image.Load(getResponse.ResponseStream);

            var top = Convert.ToInt32(documentType.BoundingBox.Top * Convert.ToDouble(img.Height));
            var height = Convert.ToInt32(documentType.BoundingBox.Height * Convert.ToDouble(img.Height));
            height = Math.Min(height, img.Height - top);
            var left = Convert.ToInt32(documentType.BoundingBox.Left * Convert.ToDouble(img.Width));
            var width = Convert.ToInt32(documentType.BoundingBox.Width * Convert.ToDouble(img.Width));
            width = Math.Min(width, img.Width - left);

            img.Mutate(i => i.Crop(new Rectangle(left, top, width, height)));

            var ms = new MemoryStream();

            img.SaveAsPng(ms);

            AWSXRayRecorder.Instance.EndSubsegment();

            var returnVal = (documentType: documentType.Type.Value, CroppedImage: ms,
                DocumentBoundingBox: documentType.BoundingBox);

            return returnVal;
        }
    }
}