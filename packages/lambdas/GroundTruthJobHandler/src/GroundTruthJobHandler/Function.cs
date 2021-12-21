using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using ThirdParty.Json.LitJson;
using CreateProjectRequest = Amazon.Rekognition.Model.CreateProjectRequest;
using S3Object = Amazon.Rekognition.Model.S3Object;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GroundTruthJobHandler
{
    public class Function
    {
        
        /// <summary>
        /// https://docs.aws.amazon.com/sagemaker/latest/dg/sms-monitor-cloud-watch.html
        /// </summary>
        public class LabelingJobChangeEventArgs
        {
            public string version { get; set; }
            public string id { get; set; }
            public string detailType { get; set; }
            public string[] resources { get; set; }

            public string account { get; set; }
            public string region { get; set; }
        }
        

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(string input, ILambdaContext context)
        {
            if (string.IsNullOrEmpty(input))
                return;

            var eventType = JsonSerializer.Deserialize<LabelingJobChangeEventArgs>(input);

            if (eventType == null || eventType.resources == null)
                return; //There's a problem with deserialization

            foreach (var gtArn in eventType.resources)
            {
                LambdaLogger.Log($"Ground truth job ${gtArn} has been marked as complete.");
                try
                {
                    await ProcessArn(gtArn);
                }
                catch (Exception ex)
                {
                    LambdaLogger.Log($"An error occurred processing {gtArn} - {ex.Message} - {ex.StackTrace}");
                }
            }
        }

        private async Task ProcessArn(string Arn)
        {
            var tableName = Environment.GetEnvironmentVariable("TrainingTableName");

            AmazonSageMakerClient sageMakerClient = new AmazonSageMakerClient();

            Amazon.S3.AmazonS3Client s3Client = new Amazon.S3.AmazonS3Client();

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = tableName,
                IndexName = "LabellingJobArn-index"
            };

            Amazon.DynamoDBv2.AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();


            DynamoDBContext dbContext = new DynamoDBContext(dynamoDbClient);

            var dbJob = dbContext.QueryAsync<TrainingJob>(Arn, config).GetRemainingAsync().GetAwaiter().GetResult()
                .FirstOrDefault();

            if (dbJob == null)
            {
                LambdaLogger.Log("Labelling job not found.");
                return;
            }

            var labellingJob = await sageMakerClient.DescribeLabelingJobAsync(new DescribeLabelingJobRequest()
            {
                LabelingJobName = dbJob.Id
            });

            string strManifestS3Key = labellingJob.OutputConfig.S3OutputPath + "/manifest/output/output.manifest";

            // Check if the manifest file exists
            AmazonS3Uri s3Uri = new AmazonS3Uri(strManifestS3Key);
            try
            {
                var s3GetResp = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                    { BucketName = s3Uri.Bucket, Key = s3Uri.Key });
            }
            catch
            {
                LambdaLogger.Log(
                    $"Output manifest file ${strManifestS3Key} does not exist or no permissions to read, cannot proceeed.");
                return;
            }

            string projectName = "ekyc-" + Convert.ToInt64(DateTime.UtcNow.Subtract(DateTime.UnixEpoch)).ToString();

            var rekognitionClient = new AmazonRekognitionClient();

            var createProjectResponse =
                await rekognitionClient.CreateProjectAsync(new CreateProjectRequest() { ProjectName = projectName });

            dbJob.ProjectArn = createProjectResponse.ProjectArn;

            var createDatasetResponse = await rekognitionClient.CreateDatasetAsync(new CreateDatasetRequest()
            {
                DatasetType = DatasetType.TRAIN, ProjectArn = createProjectResponse.ProjectArn, DatasetSource =
                    new DatasetSource()
                    {
                        GroundTruthManifest = new GroundTruthManifest()
                        {
                            S3Object = new S3Object()
                            {
                                Bucket = s3Uri.Bucket,
                                Name = s3Uri.Key
                            }
                        }
                    }
            });

            dbJob.DatasetArn = createDatasetResponse.DatasetArn;
            dbJob.Status = TrainingJobStates.CreatingDataset.ToString();

            var saveConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = tableName
            };

            await dbContext.SaveAsync(dbJob, saveConfig);
        }

        public class Detail
        {
            public string LabelingJobStatus { get; set; }
        }

        
    }
}