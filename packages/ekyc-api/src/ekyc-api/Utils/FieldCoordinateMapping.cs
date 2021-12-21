using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DocumentDefinitions;
using S3Object = Amazon.S3.Model.S3Object;

namespace ekyc_api.Utils
{
    public class FieldCoordinateMapping
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IDocumentChecker _documentChecker;
        private readonly IAmazonRekognition _rekognition;
        
        public FieldCoordinateMapping(IAmazonS3 amazonS3,IDocumentChecker documentChecker,IAmazonRekognition rekognition)
        {
            this._amazonS3 = amazonS3;
            this._documentChecker = _documentChecker;
            this._rekognition = rekognition;
        }
        
        public async Task<Dictionary<string, dynamic>> GetFieldDataByCoordinates(string S3Key)
         {
             AWSXRayRecorder.Instance.BeginSubsegment("DocumentDefinition::GetFieldValues");
 
             var tempS3Key = $"temp/{Guid.NewGuid().ToString()}.png";

             var imagingUtils = new Imaging(this._amazonS3);
 
             var croppedDocument = await imagingUtils.DetectAndCropDocument(S3Key);
 
             if (croppedDocument == null)
                 throw new Exception("Cannot ascertain the document type.");
             
             // Write the image to S3
 
             await _amazonS3.PutObjectAsync(new PutObjectRequest()
             {
                 BucketName = Globals.StorageBucket, Key = tempS3Key, InputStream = croppedDocument.Value.CroppedImage
             });
 
             var response = await _rekognition.DetectTextAsync(new DetectTextRequest
             {
                 Image = new Image
                 {
                     S3Object = new S3Object()
                     {
                         Bucket = Globals.StorageBucket,
                         Name = tempS3Key
                     }
                 }
             });
 
             var lines = response.TextDetections.Where(x => x.Type == TextTypes.LINE)
                 .OrderBy(x => x.Geometry.BoundingBox.Top)
                 .ThenBy(x => x.Geometry.BoundingBox.Left)
                 .ToList();
 
             
             foreach (var line in lines)
                 _logger.LogDebug(
                     $"{line.DetectedText} - X: {line.Geometry.BoundingBox.Left} Y: {line.Geometry.BoundingBox.Top} Width: {line.Geometry.BoundingBox.Width} Height: {line.Geometry.BoundingBox.Height}");
 
             var returnVal =new Dictionary<string, dynamic>();
             
             foreach (var field in this.DataFields)
             {
                 // Get all the extracted lines that fall within the expected bounding box of the field
                 var matchingLines = lines.Where(f =>
                         (Math.Abs(f.Geometry.BoundingBox.Left - field.ExpectedBoundingBox.Left) <=
                          Globals.BoundingBoxVarianceThreshold) &&
                         (Math.Abs(f.Geometry.BoundingBox.Top - field.ExpectedBoundingBox.Top) <=
                          Globals.BoundingBoxVarianceThreshold) &&
                         (field.ExpectedBoundingBox.Width + Globals.BoundingBoxVarianceThreshold) >
                         f.Geometry.BoundingBox.Width &&
                         (field.ExpectedBoundingBox.Height + Globals.BoundingBoxVarianceThreshold) >
                         f.Geometry.BoundingBox.Height
                     ).OrderBy(f => f.Geometry.BoundingBox.Top)
                     .ThenBy(f => f.Geometry.BoundingBox.Left)
                     .Select(f => f.DetectedText)
                     .ToArray();
 
                 if (matchingLines.Length > 0)
                     returnVal[field.Name] = string.Join(" ", matchingLines);
 
             }
 
             await S3Utils.DeleteFromS3(tempS3Key);
 
             AWSXRayRecorder.Instance.EndSubsegment();
 
             return returnVal;
         }
    }
}