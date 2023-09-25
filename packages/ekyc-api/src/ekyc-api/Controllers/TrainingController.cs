using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrainingJob = ekyc_api.DataDefinitions.TrainingJob;

namespace ekyc_api.Controllers;

[ApiController]
[Route("api/training")]
public class TrainingController : ControllerBase
{
    private readonly IAmazonDynamoDB _amazonDynamoDb;

    private readonly IAmazonS3 _amazonS3;

    private readonly IConfiguration _config;

    private readonly DynamoDBContext _dbContext;

    private readonly ILivenessChecker _livenessChecker;

    private readonly ILogger<TrainingController> _logger;

    private readonly IAmazonSageMaker _sageMaker;

    private readonly IAmazonSageMakerRuntime _sagemakerRuntime;

    private readonly SessionManager _sessionManager;


    public TrainingController(IConfiguration config, IAmazonS3 amazonS3, IAmazonDynamoDB amazonDynamoDb,
        ILivenessChecker livenessChecker, IAmazonSageMakerRuntime sagemakerRuntime, IAmazonSageMaker sageMaker,
        ILogger<TrainingController> logger)
    {
        _config = config;
        _livenessChecker = livenessChecker;
        _amazonS3 = amazonS3;
        _amazonDynamoDb = amazonDynamoDb;
        _dbContext = new DynamoDBContext(_amazonDynamoDb);
        _sessionManager = new SessionManager(_config, _amazonS3, _amazonDynamoDb);
        _sagemakerRuntime = sagemakerRuntime;
        _sageMaker = sageMaker;
        _logger = logger;
    }

    /// <summary>
    ///     Creates a new training job. Use the training job ID to access other methods for training.
    /// </summary>
    /// <returns></returns>
    [Route("create")]
    [HttpPost]
    public async Task<TrainingJob> CreateTrainingJob()
    {
        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.TrainingTableName
        };

        var job = new TrainingJob
        {
            Id = "ekyc-" + Convert.ToInt64(DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds),
            StartTime = Convert.ToInt64(DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds),
            Status = TrainingJobStates.WaitingForLabelling.ToString()
        };
        await _dbContext.SaveAsync(job, config);

        return job;
    }

    /// <summary>
    ///     Returns a list of training jobs.
    /// </summary>
    /// <returns></returns>
    [Route("list")]
    [HttpGet]
    public async Task<TrainingJob[]> ListTrainingJobs()
    {
        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.TrainingTableName
        };

        var jobs = _dbContext.ScanAsync<TrainingJob>(null, config).GetRemainingAsync().GetAwaiter().GetResult()
            .ToArray();

        jobs = jobs.OrderByDescending(a => a.StartTime).ToArray();

        return jobs;
    }

    /// <summary>
    ///     Returns a presigned URL to upload files for training.
    /// </summary>
    /// <param name="JobId"></param>
    /// <param name="S3Key"></param>
    /// <returns>A presigned URL for HTTP PUT operations. The link expires in 15 mins from request.</returns>
    /// <exception cref="HttpStatusException"></exception>
    [Route("url")]
    [HttpGet]
    public async Task<string> GetPresignedUploadUrl(string JobId, string S3Key)
    {
        if (string.IsNullOrEmpty(JobId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Job ID must be provided.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.TrainingTableName
        };

        var job = await _dbContext.LoadAsync<TrainingJob>(JobId, config);

        if (job == null)
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Job not found.");

        var realS3Key = $"images/{JobId}/{S3Key}";

        var url = _amazonS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Globals.TrainingBucket, Key = realS3Key,
            Expires = DateTime.UtcNow.AddMinutes(15),
            Verb = HttpVerb.PUT
        });

        return url;
    }

    /// <summary>
    ///     Creates a new labelling job based on the images in the S3 bucket.
    /// </summary>
    /// <returns></returns>
    [Route("start")]
    [HttpPost]
    public async Task<string> StartLabellingJob(string JobId)
    {
        if (string.IsNullOrEmpty(JobId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Job ID must be provided.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.TrainingTableName
        };

        var job = await _dbContext.LoadAsync<TrainingJob>(JobId, config);

        if (job == null)
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Job not found.");

        var strLocalManifestPath = "/tmp/" + JobId + ".json";

        var strBucketName = Globals.TrainingBucket;

        // List all the files in the S3 path

        var request = new ListObjectsV2Request
        {
            BucketName = strBucketName,
            MaxKeys = 10,
            Prefix = $"images/{JobId}/"
        };

        using (var fs = System.IO.File.Open(strLocalManifestPath, FileMode.Create))
        {
            using (var sw = new StreamWriter(fs))
            {
                ListObjectsV2Response response;

                do
                {
                    response = await _amazonS3.ListObjectsV2Async(request);

                    // Process the response.
                    foreach (var entry in response.S3Objects)
                    {
                        Console.WriteLine("key = {0} size = {1}",
                            entry.Key, entry.Size);

                        var fullS3Path =
                            $"s3://{strBucketName}/{entry.Key}";

                        sw.WriteLine(@"{""source-ref"":""" + fullS3Path + @"""}");
                    }

                    Console.WriteLine("Next Continuation Token: {0}", response.NextContinuationToken);
                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated);
            }
        }

        // Upload the manifest to S3

        var strManifestKey = "manifests/" + JobId + ".json";

        await _amazonS3.PutObjectAsync(new PutObjectRequest
            { BucketName = strBucketName, FilePath = strLocalManifestPath, Key = strManifestKey });

        // Next, create the label category JSON

        var documentTypeNames = Enum.GetNames(typeof(DocumentTypes));

        var strLocalLabelCatPath = "/tmp/" + JobId + "-cat.json";

        using (var fs = System.IO.File.OpenWrite(strLocalLabelCatPath))
        {
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(@" {

""document-version"": ""2018-11-28"",
""labels"": [
");
                var isFirst = true;
                foreach (var strDocumentType in documentTypeNames)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        sw.WriteLine(",");
                    sw.Write(@" {

""label"": """ + strDocumentType + @"""

}");
                }

                sw.WriteLine("]}");
            }
        }

        var strCategoryFileKey = "category/" + JobId + ".json";

        // Upload the category JSON
        await _amazonS3.PutObjectAsync(new PutObjectRequest
            { BucketName = strBucketName, FilePath = strLocalLabelCatPath, Key = strCategoryFileKey });


        // Create the SageMaker Ground Truth labelling job
        //public const string UiTemplateS3Uri = "label-instructions.html";

        CreateLabelingJobResponse labellingResponse = null;

        try
        {
            labellingResponse = await _sageMaker.CreateLabelingJobAsync(
                new CreateLabelingJobRequest
                {
                    HumanTaskConfig = new HumanTaskConfig
                    {
                        AnnotationConsolidationConfig = new AnnotationConsolidationConfig
                        {
                            AnnotationConsolidationLambdaArn =
                                "arn:aws:lambda:ap-southeast-1:377565633583:function:ACS-BoundingBox"
                        },
                        NumberOfHumanWorkersPerDataObject = 1,
                        PreHumanTaskLambdaArn =
                            "arn:aws:lambda:ap-southeast-1:377565633583:function:PRE-BoundingBox",
                        TaskDescription = $"Labelling for Document Bounding Boxes job Id {JobId}",
                        TaskTimeLimitInSeconds = 28800,
                        TaskTitle = $"Labelling for Document Bounding Boxes job Id {JobId}",
                        UiConfig = new UiConfig
                        {
                            UiTemplateS3Uri = Globals.GroundTruthUiTemplateS3Uri
                        },
                        WorkteamArn = Globals.GroundTruthWorkteamArn
                    },
                    InputConfig = new LabelingJobInputConfig
                    {
                        DataSource = new LabelingJobDataSource
                        {
                            S3DataSource = new LabelingJobS3DataSource
                                { ManifestS3Uri = $"s3://{strBucketName}/{strManifestKey}" }
                        }
                    },
                    LabelAttributeName = "doctype",
                    LabelCategoryConfigS3Uri = $"s3://{strBucketName}/{strCategoryFileKey}",
                    LabelingJobName = JobId,
                    OutputConfig = new LabelingJobOutputConfig
                    {
                        S3OutputPath = $"s3://{strBucketName}/output"
                    },
                    RoleArn = Globals.GroundTruthRoleArn
                });

            var arn = labellingResponse.LabelingJobArn;

            job.LabellingJobArn = arn;

            await _dbContext.SaveAsync(job, config);

            return arn;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error,
                $"An error occurred trying to create the SageMaker labelling job: {ex.Message}");
            throw;
        }
    }
}