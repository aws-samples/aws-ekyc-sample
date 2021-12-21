using Amazon.DynamoDBv2.DataModel;

namespace ekyc_api.DataDefinitions
{
    [DynamoDBTable("verificationhistory")]
    public class VerificationHistoryItem
    {
        [DynamoDBHashKey("SessionId")] public string Id { get; set; }

        [DynamoDBProperty] public string Client { get; set; }

        [DynamoDBProperty] public long Timestamp { get; set; }

        [DynamoDBProperty] public bool IsSuccessful { get; set; }

        [DynamoDBProperty] public string documentType { get; set; }

        [DynamoDBProperty] public string Error { get; set; }
    }
}