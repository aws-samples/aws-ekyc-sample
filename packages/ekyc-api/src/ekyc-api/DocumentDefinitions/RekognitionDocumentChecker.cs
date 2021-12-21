using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using BoundingBox = ekyc_api.DataDefinitions.BoundingBox;
using Landmark = ekyc_api.DataDefinitions.Landmark;

namespace ekyc_api.DocumentDefinitions
{
    public class RekognitionDocumentChecker : IDocumentChecker
    {
        private readonly IConfiguration _configuration;

        private readonly IAmazonRekognition _rekognition;


        public RekognitionDocumentChecker(IAmazonRekognition rekognition, IConfiguration _configuration)
        {
            _rekognition = rekognition;
            this._configuration = _configuration;
        }

        public async Task<DocumentTypeCheckResponse> GetDocumentType(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("RekognitionDocumentChecker:GetDocumentType");

            Debug.WriteLine($"Getting document type for {s3Key}");

            if (!string.IsNullOrEmpty(Globals.RekognitionCustomLabelsProjectArn) && !string.IsNullOrEmpty(Globals.RekognitionCustomLabelsProjectVersionArn))
            {
                // Can't check this document as there is no Rekognition Custom Labels project running
                var projects = await _rekognition.DescribeProjectVersionsAsync(new DescribeProjectVersionsRequest()
                    { ProjectArn = Globals.RekognitionCustomLabelsProjectArn });

                var projectVersion = projects.ProjectVersionDescriptions.Where(a => a.Status == "RUNNING")
                    .OrderByDescending(a => a.CreationTimestamp).FirstOrDefault();

                if (projectVersion==null)
                    return new DocumentTypeCheckResponse { Type = null, BoundingBox = null };
            }
            else
            {
                return new DocumentTypeCheckResponse { Type = null, BoundingBox = null };
            }

            var response = await _rekognition.DetectCustomLabelsAsync(new DetectCustomLabelsRequest
            {
                Image = new Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = s3Key
                    }
                },
                MinConfidence = Convert.ToSingle(Globals.GetMinimumConfidence()),
                ProjectVersionArn = Globals.RekognitionCustomLabelsProjectVersionArn
            });

            var uniqueLabels = response?.CustomLabels?
                .OrderByDescending(a => a.Confidence)
                .ThenByDescending(a => a.Geometry.BoundingBox.Height)
                .ThenByDescending(a => a.Geometry.BoundingBox.Width)
                .GroupBy(a => a.Name)
                .Select(a => a.First())
                .ToList();

            if (uniqueLabels == null || uniqueLabels.Count == 0)
                return new DocumentTypeCheckResponse { Type = null, BoundingBox = null };

            foreach (var label in uniqueLabels)
            {
                DocumentTypes detectedLabelName;

                if (Enum.TryParse(label.Name, true, out detectedLabelName))
                {
                    var bb = BoundingBox.GetBoundingBoxFromRekognition(label.Geometry.BoundingBox);

                    return new DocumentTypeCheckResponse { Type = detectedLabelName, BoundingBox = bb };
                    ;
                }
            }

            AWSXRayRecorder.Instance.EndSubsegment();

            // No label found
            return new DocumentTypeCheckResponse { Type = null, BoundingBox = null };
        }

        public async Task<List<Landmark>> GetLandmarks(string s3Key, IDocumentDefinition documentDefinition)
        {
            var expectedLandmarks = documentDefinition.Landmarks;

            if (string.IsNullOrEmpty(documentDefinition.RekognitionCustomLabelsProjectArn))
                return new List<Landmark>();

            if ((expectedLandmarks?.Length ?? 0) == 0)
                return new List<Landmark>();

            var response = await _rekognition.DetectCustomLabelsAsync(new DetectCustomLabelsRequest
            {
                Image = new Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = s3Key
                    }
                },
                ProjectVersionArn = documentDefinition.RekognitionCustomLabelsProjectArn
            });

            var lstLandmarks = new List<Landmark>();

            foreach (var label in response.CustomLabels)
            {
                var expectedLandmark = expectedLandmarks.FirstOrDefault(a => string.Equals(a.Name, label.Name));

                if (expectedLandmark != null)
                {
                    if (expectedLandmark.ExpectedBoundingBox != null)
                    {
                        // Need to check if the label lands within the bounding box
                        if (Math.Abs(expectedLandmark.ExpectedBoundingBox.Left - label.Geometry.BoundingBox.Left) >
                            Globals.BoundingBoxVarianceThreshold)
                            continue;

                        if (Math.Abs(expectedLandmark.ExpectedBoundingBox.Top - label.Geometry.BoundingBox.Top) >
                            Globals.BoundingBoxVarianceThreshold)
                            continue;

                        var expectedBottom = expectedLandmark.ExpectedBoundingBox.Top +
                                             expectedLandmark.ExpectedBoundingBox.Height;

                        var expectedRight = expectedLandmark.ExpectedBoundingBox.Left +
                                            expectedLandmark.ExpectedBoundingBox.Width;

                        var labelBottom = label.Geometry.BoundingBox.Top + label.Geometry.BoundingBox.Height;

                        var labelRight = label.Geometry.BoundingBox.Left + label.Geometry.BoundingBox.Width;

                        if (Math.Abs(expectedBottom - labelBottom) > Globals.BoundingBoxVarianceThreshold)
                            continue;

                        if (Math.Abs(expectedRight - labelRight) > Globals.BoundingBoxVarianceThreshold)
                            continue;
                    }

                    lstLandmarks.Add(new Landmark
                    {
                        Name = label.Name,
                        BoundingBox = BoundingBox.GetBoundingBoxFromRekognition(label.Geometry.BoundingBox)
                    });
                }
            }

            return lstLandmarks;
        }
    }
}