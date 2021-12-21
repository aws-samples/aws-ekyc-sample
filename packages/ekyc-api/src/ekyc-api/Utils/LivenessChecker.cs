using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using Microsoft.Extensions.Configuration;
using Image = SixLabors.ImageSharp.Image;
using Landmark = Amazon.Rekognition.Model.Landmark;

namespace ekyc_api.Utils
{
    public class LivenessChecker : ILivenessChecker
    {
        private readonly IAmazonRekognition _amazonRekognition;
        private readonly IAmazonS3 _amazonS3;
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly DynamoDBContext dynamoDbContext;
        private FaceDetail _eyesClosedFaceDetail;
        private ILivenessChecker _livenessChecker;
        private float _minConfidence;
        private IDocumentChecker _documentChecker;

        public LivenessChecker(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
            IAmazonDynamoDB dynamoDbClient, IDocumentChecker documentChecker)
        {
            _amazonRekognition = rekognition;
            _amazonS3 = s3;
            _dynamoDbClient = dynamoDbClient;
            dynamoDbContext = new DynamoDBContext(_dynamoDbClient);
            _documentChecker = documentChecker;
        }


        /// <summary>
        ///     Compares 2 faces stored in S3.
        /// </summary>
        /// <param name="sourceKey"></param>
        /// <param name="targetKey"></param>
        /// <returns></returns>
        public async Task<(bool IsMatch, float Confidence)> CompareFaces(string sourceKey, string targetKey)
        {
            var compareResponse = await _amazonRekognition.CompareFacesAsync(new CompareFacesRequest
            {
                SourceImage = new Amazon.Rekognition.Model.Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = sourceKey
                    }
                },
                TargetImage = new Amazon.Rekognition.Model.Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = targetKey
                    }
                }
            });

            if (compareResponse?.FaceMatches?.Count == 0)
                return new ValueTuple<bool, float>(false, 0);

            var match = compareResponse?.FaceMatches.Where(a => a.Similarity >= Globals.GetMinimumConfidence())
                .OrderByDescending(a => a.Similarity)
                .First();

            return new ValueTuple<bool, float>(true, match.Similarity);
        }

        public async Task<bool> VerifyImageSize(Image img)
        {
            if (img.Height < Globals.MinimumImageHeight)
                throw new HttpStatusException(HttpStatusCode.InternalServerError,
                    $"Image height must be at least {Globals.MinimumImageHeight}px");

            if (img.Width < Globals.MinimumImageWidth)
                throw new HttpStatusException(HttpStatusCode.InternalServerError,
                    $"Image width must be at least {Globals.MinimumImageWidth}px");

            return true;
        }

        /// <summary>
        ///     Verifies if a face stored in S3 has open eyes.
        /// </summary>
        /// <param name="S3Key"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<bool> VerifyEyesOpen(string S3Key)
        {
            var response = await _amazonRekognition.DetectFacesAsync(new DetectFacesRequest
            {
                Image = new Amazon.Rekognition.Model.Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = S3Key
                    }
                },
                Attributes = new List<string>(new[] { "ALL" })
            });

            if (response.FaceDetails == null || response.FaceDetails.Count == 0)
                throw new Exception("No faces found in image");

            if (response.FaceDetails[0].EyesOpen.Confidence < Globals.GetMinimumConfidence())
                throw new Exception("Not enough confidence to ascertain if eyes are open.");

            return response.FaceDetails[0].EyesOpen.Value;
        }

        public async Task<bool> VerifyFaceIsInCentre(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::VerifyFaceIsInCentre");

            var response = await _amazonRekognition.DetectFacesAsync(new DetectFacesRequest
            {
                Image = new Amazon.Rekognition.Model.Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = s3Key
                    }
                },
                Attributes = new List<string>(new[] { "DEFAULT" })
            });

            if (response == null || response.FaceDetails == null || response.FaceDetails.Count != 1)
                return false;

            var faceDetail = response.FaceDetails[0];

            var faceMidpointX = faceDetail.BoundingBox.Left + faceDetail.BoundingBox.Width / 2f;
            var faceMidpointY = faceDetail.BoundingBox.Top + faceDetail.BoundingBox.Height / 2f;
            var XDrift = Math.Abs(faceMidpointX - 0.5f);
            var YDrift = Math.Abs(faceMidpointY - 0.5f);

            if (XDrift > Globals.FaceMaxDriftFromCentre)
                return false;

            if (YDrift > Globals.FaceMaxDriftFromCentre)
                return false;

            AWSXRayRecorder.Instance.EndSubsegment();

            return true;
        }

        /// <summary>
        ///     Verifies the liveness of an image by comparing the selfie, document, nose-pointing and eyes closed images.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<string> VerifyImageLiveness(string sessionId)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::VerifyImageLiveness");

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = Globals.SessionTableName
            };

            var session = await dynamoDbContext.LoadAsync<SessionObject>(sessionId, config);

            var imaging = new Imaging(_amazonS3, _documentChecker);

            Image nosePointImage;

            if (session == null)
                return $"Session with ID {sessionId} is not found.";

            if (string.IsNullOrEmpty(session.documentImageKey))
                return $"Document to verify for session {sessionId} is empty.";

            var documentFaceCount = await GetFaceCount(session.documentImageKey, 3);

            if (documentFaceCount == 0)
                return $"No faces found on the document to verify for session {sessionId}.";

            var documentToVerifyImage = await imaging.GetImageFromStorage(session.documentImageKey);

            if (documentToVerifyImage == null)
                return $"Invalid document to verify for session {sessionId}.";


            if (string.IsNullOrEmpty(session.eyesClosedImageKey))
                return $"Eyes closed image for session {sessionId} is empty.";


            var eyesClosedFacesCount = await GetFaceCount(session.eyesClosedImageKey, 3);

            if (eyesClosedFacesCount == 0)
                return $"No faces found on the eyes closed image for session {sessionId}.";

            var eyesClosedImage = await imaging.GetImageFromStorage(session.eyesClosedImageKey);

            if (eyesClosedImage == null)
                return $"Invalid eyes closed image for session {sessionId}.";

            FaceDetail selfieFaceDetails;

            if (string.IsNullOrEmpty(session.selfieImageKey))
                return $"Selfie image for session {sessionId} is empty.";

            var selfieFaceCount = await GetFaceCount(session.selfieImageKey, 2);

            if (selfieFaceCount == 0 || selfieFaceCount > 1)
                return $"There can only be one face on the selfie image for session {sessionId}.";

            var headOnImage = await imaging.GetImageFromStorage(session.selfieImageKey);

            if (headOnImage == null)
                return $"Invalid selfie image for session {sessionId}.";

            selfieFaceDetails = GetFaces(session.selfieImageKey).GetAwaiter().GetResult().FirstOrDefault();

            var selfiePoseError = await VerifySelfieFacePose(selfieFaceDetails);

            if (selfiePoseError != null)
                return selfiePoseError;


            if (string.IsNullOrEmpty(session.nosePointImageKey))
                return $"Nose verification image for session {sessionId} is empty.";


            var nosePointFaceCount = await GetFaceCount(session.nosePointImageKey);

            if (nosePointFaceCount == 0)
                return $"No faces found on the nose point image for session {sessionId}.";

            nosePointImage = await imaging.GetImageFromStorage(session.nosePointImageKey);

            if (nosePointImage == null)
                return $"Invalid nose verification image for session {sessionId}.";


            // Make sure the selfie image matches the document image

            var similarFaces = await CompareFaces(session.selfieImageKey, session.documentImageKey);

            if (!similarFaces.IsMatch)
                return $"The face on the document does not meet the selfie for session {sessionId}.";

            // Check that the eyes are closed on the eyes closed image

            similarFaces = await CompareFaces(session.eyesClosedImageKey, session.selfieImageKey);

            if (!similarFaces.IsMatch)
                return $"Eyes closed image does not match the selfie image for session {sessionId}";

            var eyesClosed = await VerifyClosedEyes(session.eyesClosedImageKey);

            if (!eyesClosed)
                return $"Eyes are not closed for the verification image for session {sessionId}";

            // Check that the nose is in the area of the image

            similarFaces = await CompareFaces(session.nosePointImageKey, session.selfieImageKey);

            if (!similarFaces.IsMatch)
                return $"Nose verification image does not match the selfie image for session {sessionId}";

            var noseVerifyResponse =
                VerifyNoseLocation(session, nosePointImage, selfieFaceDetails).GetAwaiter().GetResult();

            AWSXRayRecorder.Instance.EndSubsegment();

            return noseVerifyResponse;
        }

        public async Task<string> VerifySelfieFacePose(string s3Key)
        {
            var face = await GetFaces(s3Key);

            if (face.Count == 0)
                return "No faces found in selfie image.";

            return await VerifySelfieFacePose(face[0]);
        }

        private async Task<int> GetFaceCount(string s3Key, int maxFaces = 1)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::GetFaceCount");

            var response = await _amazonRekognition.DetectFacesAsync(new DetectFacesRequest
            {
                Image = new Amazon.Rekognition.Model.Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = s3Key
                    }
                }
            });

            var confidence = response.FaceDetails.OrderBy(a => a.Confidence).Select(a => a.Confidence).FirstOrDefault();

            if (confidence < _minConfidence)
                _minConfidence = confidence;

            AWSXRayRecorder.Instance.EndSubsegment();

            return response.FaceDetails!.Count;
        }

        private async Task<List<FaceDetail>> GetFaces(string s3Key, int maxFaces = 1)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::GetFaces");

            var response = await _amazonRekognition.DetectFacesAsync(new DetectFacesRequest
            {
                Image = new Amazon.Rekognition.Model.Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = s3Key
                    }
                },
                Attributes = new List<string> { "ALL" }
            });

            if (response == null || response.FaceDetails == null)
                return new List<FaceDetail>();

            var confidence = response.FaceDetails.OrderBy(a => a.Confidence).Select(a => a.Confidence).FirstOrDefault();

            if (confidence < _minConfidence)
                _minConfidence = confidence;

            if (response.FaceDetails.Count > maxFaces)
                throw new Exception($"Expected {maxFaces} in image but got {response.FaceDetails.Count}");

            AWSXRayRecorder.Instance.EndSubsegment();

            return response.FaceDetails;
        }

        private async Task<string> VerifyNoseLocation(SessionObject session, Image imageToVerify,
            FaceDetail headOnImage)
        {
            var nosePointingFaces = await GetFaces(session.nosePointImageKey);

            if (nosePointingFaces.Count == 0)
                return $"No faces found in the nose verification image for session {session.Id}";

            // Get the largest face
            var nosePointingFace = nosePointingFaces.OrderByDescending(a => a.BoundingBox.Width + a.BoundingBox.Height)
                .FirstOrDefault();

            var noseLocation = nosePointingFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.Nose);

            if (noseLocation == null)
                return "Nose not found in image.";

            // Check if it's in the bounds 

            if (noseLocation.X >= session.nosePointAreaLeft &&
                noseLocation.X <= session.nosePointAreaLeft + Globals.NosePointAreaDimensions)
            {
                if (noseLocation.Y >= session.nosePointAreaTop &&
                    noseLocation.Y <= session.nosePointAreaTop + Globals.NosePointAreaDimensions)
                {
                    // Check the yaw, pitch and roll of the face

                    var compareLandmarks = await CompareFacialLandmarks(session, nosePointingFace, headOnImage);

                    if (!compareLandmarks)
                        return
                            "Please do not move your shoulders while pointing your nose. Instead, tilt your head so that your nose fits within the bounds of the box.";
                }
                else
                {
                    return "Nose is not within the specified bounds.";
                }
            }
            else
            {
                return "Nose is not within the specified bounds.";
            }

            return null;
        }

        private async Task<string> VerifySelfieFacePose(FaceDetail selfieFaceDetail)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::VerifySelfieFacePose");

            var strError = new StringBuilder();


            if (selfieFaceDetail?.Pose?.Pitch > 20)
                strError.Append("Your chin is too high, please lower it.");
            else if (selfieFaceDetail?.Pose?.Pitch < -20)
                strError.Append("Your chin is too low, please raise it.");

            if (selfieFaceDetail?.Pose?.Yaw > 20 || selfieFaceDetail?.Pose?.Yaw < -20)
                strError.Append("Please make sure your face is facing the camera straight on.");

            if (selfieFaceDetail?.Pose?.Roll > 20 || selfieFaceDetail?.Pose?.Roll < -20)
                strError.Append("Please do not tilt your head left or right.");

            if (strError.Length > 0)
                return strError.ToString();

            AWSXRayRecorder.Instance.EndSubsegment();

            return null;
        }

        private double GetTangentDegreesFromXY(double width, double height)
        {
            var radians = Math.Atan(width / height);
            var angle = radians * (180 / Math.PI);
            return angle;
        }

        private double GetTangentDegreesBetweenLandmarks(Landmark l1, Landmark l2)
        {
            var width = Math.Abs(l1.X - l2.X);
            var height = Math.Abs(l1.Y - l2.Y);

            var angle = GetTangentDegreesFromXY(width, height);

            return angle;
        }

        private async Task<bool> CompareFacialLandmarks(SessionObject session, FaceDetail nosePointingFace,
            FaceDetail selfieFace)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::VerifyImageLiveness");

            /*      var npfMouthRight = nosePointingFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.MouthRight);
                var npfMouthLeft = nosePointingFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.MouthLeft);
                var npfNose =  nosePointingFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.Nose);
                var npfLeftEye = nosePointingFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.EyeLeft);
                var npfRightEye = nosePointingFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.EyeRight);
    
             
                  
                var selfieMouthRight = selfieFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.MouthRight);
                var selfieMouthLeft = nosePointingFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.MouthLeft);
                var selfieNose = selfieFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.Nose);
                var selfieLeftEye = selfieFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.EyeLeft);
                var selfieRightEye = selfieFace.Landmarks.FirstOrDefault(a => a.Type == LandmarkType.EyeRight);
    
                if (npfMouthLeft == null)
                    throw new Exception("Unable to find the left corner of the mouth for the nose pointing image.");
                
                if (npfMouthRight == null)
                    throw new Exception("Unable to find the right corner of the mouth for the nose pointing image.");
                
                if (npfNose == null)
                    throw new Exception("Unable to find the nose for the nose pointing image.");
                
                if (npfRightEye == null)
                    throw new Exception("Unable to find the right eye for the nose pointing image.");
                
                if (npfLeftEye == null)
                    throw new Exception("Unable to find the left eye for the nose pointing image.");
                
                
                if (selfieMouthRight == null)
                    throw new Exception("Unable to find the right corner of the mouth for the selfie.");
                
                if (selfieMouthLeft == null)
                    throw new Exception("Unable to find the left corner of the mouth for the selfie");
                
                if (selfieNose == null)
                    throw new Exception("Unable to find the nose for the selfie.");
                
                if (npfRightEye == null)
                    throw new Exception("Unable to find the right eye for the selfie.");
                
                if (npfLeftEye == null)
                    throw new Exception("Unable to find the left eye for the selfie.");
                
              // Nose point calculation
               
                double npfMouthLeftAndNoseDegrees = GetTangentDegreesBetweenLandmarks(npfMouthLeft, npfNose);
                
                double npfMouthRightAndNoseDegrees = GetTangentDegreesBetweenLandmarks(npfMouthRight,
                    npfNose);
    
                double npfLeftEyeAndNoseDegrees = GetTangentDegreesBetweenLandmarks(npfNose, npfLeftEye);
    
                double npfRightEyeAndNoseDegrees = GetTangentDegreesBetweenLandmarks(npfNose, npfRightEye);
                
                // Selfie calculation
                
                double selfieMouthLeftAndNoseDegrees = GetTangentDegreesBetweenLandmarks(selfieMouthLeft, selfieNose);
                
                double selfieMouthRightAndNoseDegrees = GetTangentDegreesBetweenLandmarks(selfieMouthRight,
                    selfieNose);
    
                double selfieLeftEyeAndNoseDegrees = GetTangentDegreesBetweenLandmarks(selfieNose, selfieLeftEye);
    
                double selfieRightEyeAndNoseDegrees = GetTangentDegreesBetweenLandmarks(selfieNose, selfieRightEye);*/

            Console.WriteLine(
                $"Session Nose Pointing Coordinates: X - {session.nosePointAreaLeft}, Y - {session.nosePointAreaTop}");

            Console.WriteLine(
                $"Selfie Face: Yaw - {selfieFace.Pose.Yaw}, Pitch - {selfieFace.Pose.Pitch}, Roll - {selfieFace.Pose.Roll}");

            Console.WriteLine(
                $"Nose Point Face: Yaw - {nosePointingFace.Pose.Yaw}, Pitch - {nosePointingFace.Pose.Pitch}, Roll - {nosePointingFace.Pose.Roll}");


            // Top left quadrant
            if (session.nosePointAreaLeft.Value <= 0.5f && session.nosePointAreaTop.Value <= 0.5)
            {
                var poseFit = nosePointingFace.Pose.Yaw < selfieFace.Pose.Yaw &&
                              nosePointingFace.Pose.Pitch > selfieFace.Pose.Pitch;

                if (!poseFit)
                    return false;
                /*
                // We expect the tangent of the left of the mouth to the nose to be smaller

                if (npfMouthRightAndNoseDegrees >= selfieMouthRightAndNoseDegrees)
                    return false;
                
                // We expect the tangent of the left eye to the nose to be smaller
                if (npfLeftEyeAndNoseDegrees >= selfieLeftEyeAndNoseDegrees)
                    return false;
*/
            }


            // Top right quadrant
            if (session.nosePointAreaLeft > 0.5f && session.nosePointAreaTop <= 0.5f)
            {
                var poseFit = nosePointingFace.Pose.Yaw >= selfieFace.Pose.Yaw &&
                              nosePointingFace.Pose.Pitch >= selfieFace.Pose.Pitch;

                if (!poseFit)
                    return false;
                /* 
                 // We expect the tangent of the right of the mouth to the nose to be smaller
 
                 if (npfMouthLeftAndNoseDegrees >= selfieMouthLeftAndNoseDegrees)
                     return false;
                 
                 // We expect the tangent of the left eye to the nose to be smaller
                 if (npfLeftEyeAndNoseDegrees >= selfieLeftEyeAndNoseDegrees)
                     return false;
                     */
            }

            // Bottom left quadrant
            if (session.nosePointAreaLeft <= 0.5f && session.nosePointAreaTop > 0.5f)
            {
                var poseFit = nosePointingFace.Pose.Yaw < selfieFace.Pose.Yaw &&
                              nosePointingFace.Pose.Pitch < selfieFace.Pose.Pitch;

                if (!poseFit)
                    return false;
            }


            // Bottom right quadrant
            if (session.nosePointAreaLeft > 0.5f && session.nosePointAreaTop >= 0.5f)
            {
                var poseFit = nosePointingFace.Pose.Yaw >= selfieFace.Pose.Yaw &&
                              nosePointingFace.Pose.Pitch < selfieFace.Pose.Pitch;

                if (!poseFit)
                    return false;
            }

            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::CompareFacialLandmarks");

            return true;
        }

        private async Task<bool> VerifyClosedEyes(string faceS3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("LivenessChecker::VerifyClosedEyes");

            var response = await _amazonRekognition.DetectFacesAsync(new DetectFacesRequest
            {
                Image = new Amazon.Rekognition.Model.Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = faceS3Key
                    }
                },
                Attributes = new List<string> { "ALL" }
            });

            if (response.FaceDetails != null && response.FaceDetails.Count > 0)
            {
                _eyesClosedFaceDetail = response.FaceDetails[0];
                return _eyesClosedFaceDetail.EyesOpen.Value == false;
            }

            AWSXRayRecorder.Instance.EndSubsegment();


            return false;
        }
    }
}