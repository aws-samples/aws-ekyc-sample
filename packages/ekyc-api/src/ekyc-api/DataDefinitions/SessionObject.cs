using Amazon.DynamoDBv2.DataModel;

namespace ekyc_api.DataDefinitions;

public class CreateSessionResponse
{
    public string livenessSessionId { get; set; }

    public string sessionId { get; set; }
}

public class GetLivenessCheckResponse
{
    public string LivenessCheckSessionId { get; set; }

    public bool Verified { get; set; }

    public float Confidence { get; set; }

    public string SessionId { get; set; }
}

[DynamoDBTable("sessions")]
public class SessionObject
{
    [DynamoDBHashKey] public string Id { get; set; }

    [DynamoDBProperty] public long expiry { get; set; }

    [DynamoDBProperty] public string documentType { get; set; }

    [DynamoDBProperty] public string client { get; set; }
    [DynamoDBProperty] public string documentBoundingBox { get; set; }

    [DynamoDBProperty] public string documentImageKey { get; set; }

    [DynamoDBProperty] public string nosePointImageKey { get; set; }

    [DynamoDBProperty] public string selfieImageKey { get; set; }

    [DynamoDBProperty] public string eyesClosedImageKey { get; set; }

    [DynamoDBProperty] public double? nosePointAreaTop { get; set; }

    [DynamoDBProperty] public double? nosePointAreaLeft { get; set; }

    [DynamoDBProperty] public string rekognitionLivenessSessionId { get; set; }

    [DynamoDBProperty] public string rekognitionLivenessResponse { get; set; }

    [DynamoDBProperty] public bool isLive { get; set; }
}