using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.SageMaker;
using Amazon.SageMakerRuntime;
using ekyc_api.Controllers;
using ekyc_api.DocumentDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ekyc_api_tests
{
    public class TrainingTests : TestBase
    {
        [SetUp]
        public void SetUp()
        {
            // put other startup code here
            _livenessControllerlogger = TestHost.Services.GetService<ILogger<LivenessController>>();
            _documentControllerLogger = TestHost.Services.GetService<ILogger<DocumentController>>();
            _trainingControllerlogger = TestHost.Services.GetService<ILogger<TrainingController>>();
            _dataControllerLogger = TestHost.Services.GetService<ILogger<DataController>>();
            _amazonDynamoDb = TestHost.Services.GetService<IAmazonDynamoDB>();
            _dbContext = new DynamoDBContext(_amazonDynamoDb);
            _config = TestHost.Services.GetService<IConfiguration>();
            _s3Client = TestHost.Services.GetService<IAmazonS3>();
            _factory = TestHost.Services.GetService<IDocumentDefinitionFactory>();
            _documentChecker = TestHost.Services.GetService<IDocumentChecker>();
            _dataController = new DataController(_config, _factory, _s3Client, _amazonDynamoDb, _documentChecker,
                _dataControllerLogger);
            _livenessChecker = TestHost.Services.GetService<ILivenessChecker>();
            _sagemaker = TestHost.Services.GetService<IAmazonSageMaker>();
            _sagemakerRuntime = TestHost.Services.GetService<IAmazonSageMakerRuntime>();
        }

        private ILivenessChecker _livenessChecker;

        private ILogger<LivenessController> _livenessControllerlogger;

        private ILogger<TrainingController> _trainingControllerlogger;

        private ILogger<DocumentController> _documentControllerLogger;

        private ILogger<DataController> _dataControllerLogger;

        private IConfiguration _config;

        private DataController _dataController;

        private IAmazonS3 _s3Client;

        private IAmazonDynamoDB _amazonDynamoDb;

        private IDocumentDefinitionFactory _factory;

        private IDocumentChecker _documentChecker;

        private SessionController _sessionController;

        private IAmazonSageMakerRuntime _sagemakerRuntime;

        private IAmazonSageMaker _sagemaker;

        private DynamoDBContext _dbContext;


        [Test]
        public async Task TestCreateAndStartJob()
        {
            var controller = new TrainingController(_config, _s3Client, _amazonDynamoDb, _livenessChecker,
                _sagemakerRuntime, _sagemaker, _trainingControllerlogger);

            var job = await controller.CreateTrainingJob();

            Console.WriteLine(job);

            DirectoryInfo di = new DirectoryInfo($"../../../SampleData/myKAD");

            foreach (var file in di.GetFiles())
            {
                var url = await controller.GetPresignedUploadUrl(job.Id, file.Name);

                await PutObjectAtUrl(url, file.FullName);
            }

            var labellingJobArn = await controller.StartLabellingJob(job.Id);

            Console.WriteLine($"Job created - {labellingJobArn}");
        }
    }
}