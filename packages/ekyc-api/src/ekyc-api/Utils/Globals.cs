using System;
using System.Security.Cryptography;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace ekyc_api.Utils
{
    public class Globals
    {
        public static double MinDocumentHeight = 0.5d;

        public static double MinDocumentWidth = 0.5d;

        public static double BoundingBoxVarianceThreshold => 0.05d;

        public const string CorsPolicyName = "SpecificCorsPolicy";

        public static string StorageBucket => Environment.GetEnvironmentVariable("StorageBucket");

        public static string SessionTableName => Environment.GetEnvironmentVariable("SessionTable");

        public static string VerificationHistoryTableName =>
            Environment.GetEnvironmentVariable("VerificationHistoryTable");

        public static string DataRequestsTableName =>
            Environment.GetEnvironmentVariable("DataRequestsTable");

        public static string TrainingTableName =>
            Environment.GetEnvironmentVariable("TrainingTableName");
        
        public static string OcrServiceEndpoint => 
            Environment.GetEnvironmentVariable("OcrServiceEndpoint");

        public static string GroundTruthUiTemplateS3Uri =>
            Environment.GetEnvironmentVariable("GroundTruthUiTemplateS3Uri");

        public static string TrainingBucket => Environment.GetEnvironmentVariable("TrainingBucket");

        public static string GroundTruthRoleArn => Environment.GetEnvironmentVariable("GroundTruthRoleArn");

        public static string GroundTruthWorkteamArn => Environment.GetEnvironmentVariable("GroundTruthWorkTeam");

        public static string RekognitionCustomLabelsProjectArn
        {
            get
            {
                var strParamName = Environment.GetEnvironmentVariable("RekognitionCustomLabelsProjectArnParameterName");
                AmazonSimpleSystemsManagementClient client = new AmazonSimpleSystemsManagementClient();
                var response = client.GetParameterAsync(new GetParameterRequest()
                    { Name = strParamName }
                ).GetAwaiter().GetResult();

                if (response.Parameter?.Value == "default")
                    return null;
                else
                    return response.Parameter?.Value;
            }
        }
        
        public static string RekognitionCustomLabelsProjectVersionArn
        {
            get
            {
                var strParamName = Environment.GetEnvironmentVariable("RekognitionCustomLabelsProjectVersionArnParameterName");
                AmazonSimpleSystemsManagementClient client = new AmazonSimpleSystemsManagementClient();
                var response = client.GetParameterAsync(new GetParameterRequest()
                    { Name = strParamName }
                ).GetAwaiter().GetResult();

                if (response.Parameter?.Value == "default")
                    return null;
                else
                    return response.Parameter?.Value;
            }
        }
        
        public static bool UseFieldCoordinatesExtractionMethod
        {
            get
            {
                var strParamName = Environment.GetEnvironmentVariable("UseFieldCoordinatesExtractionMethodParameterName");
                AmazonSimpleSystemsManagementClient client = new AmazonSimpleSystemsManagementClient();
                var response = client.GetParameterAsync(new GetParameterRequest()
                    { Name = strParamName }
                ).GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(response.Parameter?.Value))
                    return false;
                else
                {
                    bool returnVal;

                    var strValue = response.Parameter?.Value;

                    if (Boolean.TryParse(strValue, out returnVal))
                        return returnVal;
                    else
                    {
                        return false;
                    }
                }

            }
        }


        public static string ApprovalsSnsTopic => Environment.GetEnvironmentVariable("ApprovalsSnsTopic");

        public static int MinimumImageHeight => 250;

        public static double NosePointAreaDimensions => 0.05d;

        public static double FaceMaxDriftFromCentre => 0.05d;

        public static int MinimumImageWidth => 250;

        /// <summary>
        ///     Gets the minimum confidence that should be applied to calls to AI services.
        /// </summary>
        /// <returns></returns>
        public static double GetMinimumConfidence()
        {
            var strConfidence = Environment.GetEnvironmentVariable("MinConfidence");

            if (string.IsNullOrEmpty(strConfidence))
                return 70d;

            double dblConfidence;

            if (double.TryParse(strConfidence, out dblConfidence))
                return dblConfidence;
            return 70d;
        }
    }
}