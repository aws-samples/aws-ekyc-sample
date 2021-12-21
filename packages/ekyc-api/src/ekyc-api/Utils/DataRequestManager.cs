using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using ekyc_api.DataDefinitions;
using Microsoft.Extensions.Configuration;

namespace ekyc_api.Utils
{
    public class DataRequestManager
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly DynamoDBContext dynamoDbContext;
        private IConfiguration config;


        public DataRequestManager(IConfiguration config, IAmazonS3 s3, IAmazonDynamoDB dynamoDbClient)
        {
            this.config = config;
            _amazonS3 = s3;
            _dynamoDbClient = dynamoDbClient;
            dynamoDbContext = new DynamoDBContext(_dynamoDbClient);
        }

        public async Task<bool> DataRequestExistsAndIsValid(string Id)
        {
            if (string.IsNullOrEmpty(Id))
                return false;

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = Globals.DataRequestsTableName
            };

            var request = await dynamoDbContext.LoadAsync<DataRequest>(Id, config);

            if (request == null)
                return false;

            /*  var currentTime = Convert.ToInt64(DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds);
  
              if (request.expiry < currentTime)
                  return false; */

            return true;
        }

        public async Task<DataRequest> CreateNewRequest(string UserAgent)
        {
            var newDataRequest = new DataRequest();
            newDataRequest.Id = Guid.NewGuid().ToString();
            newDataRequest.expiry =
                Convert.ToInt64(DateTime.UtcNow.AddHours(1).Subtract(DateTime.UnixEpoch).TotalSeconds);
            newDataRequest.createdAt = DateTime.UtcNow;
            newDataRequest.UserAgent = UserAgent;

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = Globals.DataRequestsTableName
            };

            await dynamoDbContext.SaveAsync(newDataRequest, config);

            return newDataRequest;
        }
    }
}