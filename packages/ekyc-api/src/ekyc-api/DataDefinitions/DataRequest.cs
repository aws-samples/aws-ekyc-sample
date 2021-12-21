using System;
using Amazon.DynamoDBv2.DataModel;

namespace ekyc_api.DataDefinitions
{
    [DynamoDBTable("DataRequests")]
    public class DataRequest
    {
        [DynamoDBHashKey] public string Id { get; set; }

        [DynamoDBProperty] public long expiry { get; set; }

        [DynamoDBProperty] public DateTime createdAt { get; set; }

        [DynamoDBProperty] public string UserAgent { get; set; }
    }
}