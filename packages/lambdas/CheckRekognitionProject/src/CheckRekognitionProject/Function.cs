using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.SageMaker;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CheckDatasetHandler
{
    public class Function
    {
        string TrainingJobsTableName = System.Environment.GetEnvironmentVariable("TrainingTableName");

        /// <summary>
        /// Checks if there are any Rekognition datasets that have finished training
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(string input, ILambdaContext context)
        {
            // Check if there any training jobs that need to be updated


            AmazonSageMakerClient sageMakerClient = new AmazonSageMakerClient();

            Amazon.S3.AmazonS3Client s3Client = new Amazon.S3.AmazonS3Client();

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = TrainingJobsTableName
            };

            Amazon.DynamoDBv2.AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();

            DynamoDBContext dbContext = new DynamoDBContext(dynamoDbClient);

            var trainingJobs = dbContext.ScanAsync<TrainingJob>(new ScanCondition[] { }, config).GetRemainingAsync()
                .GetAwaiter().GetResult();

            foreach (var job in trainingJobs)
            {
                TrainingJobStates status;

                if (Enum.TryParse(job.Status, out status))
                {
                    if (status == TrainingJobStates.CreatingDataset)
                    {
                        await HandleCreatingDatasetStatus(job);
                    }
                    else if (status == TrainingJobStates.Training)
                    {
                        await HandleTrainingStatus(job);
                    }
                    else if (status == TrainingJobStates.Deploying)
                    {
                        await HandleDeployingStatus(job);
                    }
                }
            }
        }

        private async Task HandleDeployingStatus(TrainingJob job)
        {
            Amazon.Rekognition.AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient();

            var projectVersions = await rekognitionClient.DescribeProjectVersionsAsync(
                new DescribeProjectVersionsRequest()
                    { ProjectArn = job.ProjectArn });

            var projectVersion =
                projectVersions.ProjectVersionDescriptions.FirstOrDefault(a =>
                    a.Status == ProjectVersionStatus.RUNNING &&
                    a.ProjectVersionArn == job.ProjectVersionArn);

            if (projectVersion == null)
            {
                job.DetailedStatus = "No project version found. This is most likely caused by an error.";
            }
            else
            {
                job.Status = TrainingJobStates.Ready.ToString();

                // Set the SSM parameter of the ARN so that Rekognition custom labels can start working
                var strParamName = Environment.GetEnvironmentVariable("RekognitionCustomLabelsProjectVersionArnParameterName");
                AmazonSimpleSystemsManagementClient ssmClient = new AmazonSimpleSystemsManagementClient();
                var response = await ssmClient.PutParameterAsync(new PutParameterRequest()
                {
                    Name = strParamName,
                    Value = job.ProjectVersionArn
                });
                
                strParamName = Environment.GetEnvironmentVariable("RekognitionCustomLabelsProjectArnParameterName");
                response = await ssmClient.PutParameterAsync(new PutParameterRequest()
                {
                    Name = strParamName,
                    Value = job.ProjectArn
                });
            }

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = TrainingJobsTableName
            };

            Amazon.DynamoDBv2.AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();

            DynamoDBContext dbContext = new DynamoDBContext(dynamoDbClient);

            await dbContext.SaveAsync(job, config);
        }

        private async Task HandleTrainingStatus(TrainingJob job)
        {
            Amazon.Rekognition.AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient();

            var projectVersions = await rekognitionClient.DescribeProjectVersionsAsync(
                new DescribeProjectVersionsRequest()
                    { ProjectArn = job.ProjectArn });

            var projectVersion =
                projectVersions.ProjectVersionDescriptions.FirstOrDefault(a =>
                    a.Status == ProjectVersionStatus.TRAINING_COMPLETED &&
                    a.ProjectVersionArn == job.ProjectVersionArn);

            if (projectVersion == null)
            {
                job.DetailedStatus = "No project version found. This is most likely caused by an error.";
            }
            else
            {
                var response = await rekognitionClient.StartProjectVersionAsync(new StartProjectVersionRequest()
                {
                    MinInferenceUnits = 1,
                    ProjectVersionArn = projectVersion.ProjectVersionArn
                });

                job.Status = TrainingJobStates.Deploying.ToString();
            }

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = TrainingJobsTableName
            };

            Amazon.DynamoDBv2.AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();

            DynamoDBContext dbContext = new DynamoDBContext(dynamoDbClient);

            await dbContext.SaveAsync(job, config);
        }

        private async Task HandleCreatingDatasetStatus(TrainingJob job)
        {
            Amazon.Rekognition.AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient();

            var datasetResponse = await rekognitionClient.DescribeDatasetAsync(new DescribeDatasetRequest()
                { DatasetArn = job.DatasetArn });

            job.DetailedStatus = "Creating Dataset - " + datasetResponse.DatasetDescription.Status.ToString();

            if (datasetResponse.DatasetDescription.Status == DatasetStatus.CREATE_COMPLETE ||
                datasetResponse.DatasetDescription.Status == DatasetStatus.UPDATE_COMPLETE)
            {
                // It's done, start the training

                var response = await rekognitionClient.CreateProjectVersionAsync(new CreateProjectVersionRequest()
                {
                    ProjectArn = job.ProjectArn,
                    OutputConfig = new OutputConfig()
                    {
                        S3Bucket = System.Environment.GetEnvironmentVariable("TrainingBucket"),
                        S3KeyPrefix = job.Id + "/"
                    }
                });

                job.ProjectVersionArn = response.ProjectVersionArn;

                job.DetailedStatus = "Training";

                job.Status = TrainingJobStates.Training.ToString();
            }

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = TrainingJobsTableName
            };

            Amazon.DynamoDBv2.AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();

            DynamoDBContext dbContext = new DynamoDBContext(dynamoDbClient);

            await dbContext.SaveAsync(job, config);
        }
    }
}