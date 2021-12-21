using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using ekyc_api.DataDefinitions;
using Microsoft.Extensions.Configuration;

namespace ekyc_api.Utils
{
    public class SessionManager
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly DynamoDBContext dynamoDbContext;
        private IConfiguration config;


        public SessionManager(IConfiguration config, IAmazonS3 s3, IAmazonDynamoDB dynamoDbClient)
        {
            this.config = config;
            _amazonS3 = s3;
            _dynamoDbClient = dynamoDbClient;
            dynamoDbContext = new DynamoDBContext(_dynamoDbClient);
        }

        public async Task<bool> SessionExistsAndIsValid(string sessionId)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = Globals.SessionTableName
            };

            var session = await dynamoDbContext.LoadAsync<SessionObject>(sessionId, config);

            if (session == null)
                return false;

            var currentTime = Convert.ToInt64(DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds);

            if (session.expiry < currentTime)
                return false;

            return true;
        }

        public async Task<SessionObject> CreateNewSession(string clientName)
        {
            var dbConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = Globals.SessionTableName
            };

            var randomGenerator = RandomNumberGenerator.Create(); // Compliant for security-sensitive use cases


            var data = new byte[32];
            randomGenerator.GetBytes(data);
            var keyIsUnique = false;
            string currentKey = null;

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            //var randomNumberData = new byte[4];
            // Create a cryptographically strong random number generator
            var rnd = new Random(BitConverter.ToInt32(data));

            while (!keyIsUnique)
            {
                currentKey = new string(Enumerable.Repeat(chars, 8)
                    .Select(s =>
                    {
                        
                        var idx = rnd.Next(0, chars.Length - 1);
                        return chars[idx];
                    }).ToArray());
                try
                {
                    var existingItem = await dynamoDbContext.LoadAsync<SessionObject>(currentKey, dbConfig);
                    if (existingItem == null)
                        keyIsUnique = true;
                }
                catch (AmazonDynamoDBException)
                {
                    keyIsUnique = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error when loading object {ex.Message}");
                }
            }

            var newSession = new SessionObject();
            newSession.Id = currentKey;
            newSession.expiry = Convert.ToInt64(DateTime.UtcNow.AddHours(1).Subtract(DateTime.UnixEpoch).TotalSeconds);
            newSession.client = clientName;

            var noseLeftBounds = 0.40d;
            var noseRightBounds = 0.60d;
            var noseTopBounds = 0.40d;
            var noseBottomBounds = 0.60d;


            double rndDouble = 0d;

            // Make sure the area is not too far left of the image - we also need to preserve a buffer from the right of the image
            // Keep the box close to the centre of the image so the user doesn't need to move their whole head to point
            // We also need to make sure that the box is not too close to the centre point, or else the user won't need to point

            rndDouble = Convert.ToDouble(rnd.Next(0, 100) / 100d);

            if (rndDouble > 0.5d)
            {
                rndDouble = Convert.ToDouble(rnd.Next(70, 100) / 100d);
                newSession.nosePointAreaLeft =
                    Math.Round(noseRightBounds - rndDouble * (noseRightBounds - noseLeftBounds), 3);
            }
            else
            {
                rndDouble = Convert.ToDouble(rnd.Next(70, 100) / 100d);
                newSession.nosePointAreaLeft =
                    Math.Round(noseLeftBounds + rndDouble * (noseRightBounds - noseLeftBounds), 3);
            }

            // Make sure the area is not too far to the top of the image - we also need to preserve a buffer from the bottom of the image
            // Keep the box close to the centre of the image so the user doesn't need to move their whole head to point
            // We also need to make sure that the box is not too close to the centre point, or else the user won't need to point

            rndDouble = Convert.ToDouble(rnd.Next(0, 100) / 100d);

            if (rndDouble > 0.5d)
            {
                rndDouble = Convert.ToDouble(rnd.Next(70, 100) / 100d);
                newSession.nosePointAreaTop =
                    Math.Round(noseTopBounds + rndDouble * (noseBottomBounds - noseTopBounds), 3);
            }
            else
            {
                rndDouble = Convert.ToDouble(rnd.Next(70, 100) / 100d);
                newSession.nosePointAreaTop =
                    Math.Round(noseBottomBounds - rndDouble * (noseBottomBounds - noseTopBounds), 3);
            }


            await dynamoDbContext.SaveAsync(newSession, dbConfig);

            return newSession;
        }
    }
}