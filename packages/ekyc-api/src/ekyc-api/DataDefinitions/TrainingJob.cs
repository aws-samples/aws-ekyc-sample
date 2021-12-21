using Amazon.DynamoDBv2.DataModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ekyc_api.DataDefinitions
{
    [DynamoDBTable("sessions")]
    public class TrainingJob
    {
        [DynamoDBHashKey] public string Id { get; set; }

        [DynamoDBProperty] public long StartTime { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [DynamoDBProperty]
        public string Status { get; set; }

        [DynamoDBProperty] public string DetailedStatus { get; set; }

        [DynamoDBProperty] public string LabellingJobArn { get; set; }

        [DynamoDBProperty] public bool DatasetCreated { get; set; }

        [DynamoDBProperty] public string DatasetArn { get; set; }

        [DynamoDBProperty] public string ProjectVersionArn { get; set; }

        [DynamoDBProperty] public string ProjectArn { get; set; }
    }

    public enum TrainingJobStates
    {
        Labelling,
        CreatingDataset,
        Training,
        Deploying,
        Ready,
        WaitingForLabelling
    }
}