using System;
using System.Collections.Generic;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Newtonsoft.Json;

namespace ekyc_api.Utils;

public class Globals
{
    public static double MinDocumentHeight = 0.5d;

    public static double MinDocumentWidth = 0.5d;

    // TODO: make this come from Parameters
    public static string ThaiIdRekognitionCustomLabelsProjectArn =
        "arn:aws:rekognition:ap-southeast-1:886995061454:project/thai-id-landmarks/version/thai-id-landmarks.2023-09-11T11.36.51/1694396211317";

    public static bool IsRunningOnLambda => Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") != null;

    public static double BoundingBoxVarianceThreshold => 0.05d;

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

    public static string OcrLambdaArn =>
        Environment.GetEnvironmentVariable("OcrLambdaArn");

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
            var client = new AmazonSimpleSystemsManagementClient();
            var response = client.GetParameterAsync(new GetParameterRequest { Name = strParamName }
            ).GetAwaiter().GetResult();

            if (response.Parameter?.Value == "default")
                return null;
            return response.Parameter?.Value;
        }
    }

    public static string RekognitionCustomLabelsProjectVersionArn
    {
        get
        {
            var strParamName =
                Environment.GetEnvironmentVariable("RekognitionCustomLabelsProjectVersionArnParameterName");
            var client = new AmazonSimpleSystemsManagementClient();
            var response = client.GetParameterAsync(new GetParameterRequest { Name = strParamName }
            ).GetAwaiter().GetResult();

            if (response.Parameter?.Value == "default")
                return null;
            return response.Parameter?.Value;
        }
    }

    public static bool UseFieldCoordinatesExtractionMethod
    {
        get
        {
            var strParamName = Environment.GetEnvironmentVariable("UseFieldCoordinatesExtractionMethodParameterName");
            var client = new AmazonSimpleSystemsManagementClient();
            var response = client.GetParameterAsync(new GetParameterRequest { Name = strParamName }
            ).GetAwaiter().GetResult();

            if (string.IsNullOrEmpty(response.Parameter?.Value)) return false;

            bool returnVal;

            var strValue = response.Parameter?.Value;

            if (bool.TryParse(strValue, out returnVal))
                return returnVal;
            return false;
        }
    }


    public static string ApprovalsSnsTopic => Environment.GetEnvironmentVariable("ApprovalsSnsTopic");

    public static int MinimumImageHeight => 250;

    public static double NosePointAreaDimensions => 0.05d;

    public static double FaceMaxDriftFromCentre => 0.05d;

    public static int MinimumImageWidth => 250;

    public static Dictionary<string, TValue> ToDictionary<TValue>(object obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, TValue>>(json);
        return dictionary;
    }

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