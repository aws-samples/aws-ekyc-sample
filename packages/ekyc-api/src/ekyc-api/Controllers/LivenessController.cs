using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.Controllers;

[ApiController]
[Route("api/liveness")]
public class LivenessController : ControllerBase
{
    private readonly IAmazonDynamoDB _amazonDynamoDb;

    private readonly IAmazonRekognition _amazonRekognition;

    private readonly IAmazonS3 _amazonS3;

    private readonly IConfiguration _config;

    private readonly DynamoDBContext _dbContext;

    private readonly ILivenessChecker _livenessChecker;
    private readonly ILogger<LivenessController> _logger;

    private readonly SessionManager _sessionManager;

    public LivenessController(IConfiguration config, IAmazonS3 amazonS3, IAmazonDynamoDB amazonDynamoDb,
        IAmazonRekognition amazonRekognition,
        ILivenessChecker livenessChecker, ILogger<LivenessController> logger)
    {
        _config = config;
        _livenessChecker = livenessChecker;
        _amazonS3 = amazonS3;
        _amazonRekognition = amazonRekognition;
        _amazonDynamoDb = amazonDynamoDb;
        _dbContext = new DynamoDBContext(_amazonDynamoDb);
        _sessionManager = new SessionManager(_config, _amazonS3, _amazonDynamoDb);
        _logger = logger;
    }

    [HttpPost]
    [Route("createsession/{sessionId}/{sessionToken}")]
    public async Task<CreateSessionResponse> CreateLivenessSession(string sessionId, string sessionToken)
    {
        // Need to hardcode the region as Liveness is not supported in all regions yet
        var rekognitionClient = new AmazonRekognitionClient(RegionEndpoint.APNortheast1);
        var response = await rekognitionClient.CreateFaceLivenessSessionAsync(new CreateFaceLivenessSessionRequest
        {
            ClientRequestToken = sessionToken,
            Settings = new CreateFaceLivenessSessionRequestSettings
            {
                AuditImagesLimit = 1
            }
        });


        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var item = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (item == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        item.rekognitionLivenessSessionId = response.SessionId;

        await _dbContext.SaveAsync(item, config);

        return new CreateSessionResponse { livenessSessionId = response.SessionId, sessionId = sessionId };
    }

    [HttpGet]
    [Route("getsessionresult/{sessionId}/{livenessSessionId}")]
    public async Task<GetLivenessCheckResponse> GetLivenessSessionResult(string sessionId, string livenessSessionId)
    {
        // Need to hardcode the region as Liveness is not supported in all regions yet
        var rekognitionClient = new AmazonRekognitionClient(RegionEndpoint.APNortheast1);
        var s3ClientAPNE1 = new AmazonS3Client(RegionEndpoint.APNortheast1);
        var response = await rekognitionClient.GetFaceLivenessSessionResultsAsync(
            new GetFaceLivenessSessionResultsRequest
            {
                SessionId = livenessSessionId
            });


        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var session = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (session == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        // Copy the selfie image
        if (response.AuditImages?.Count > 0)
        {
            // var getAuditImageResponse = await s3ClientAPNE1.GetObjectAsync(new GetObjectRequest
            // {
            //     BucketName = response.AuditImages[0].S3Object.Bucket,
            //     Key = response.AuditImages[0].S3Object.Name
            // });
            var s3Key = session.Id + "/selfie.jpg";
            var ms = new MemoryStream();
            response.AuditImages[0].Bytes.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            await _amazonS3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = Globals.StorageBucket,
                Key = s3Key,
                InputStream = ms
            });

            session.selfieImageKey = s3Key;
            await _dbContext.SaveAsync(session, config);
        }

        return new GetLivenessCheckResponse
        {
            Verified = response.Confidence > 70 && response.Status == LivenessSessionStatus.SUCCEEDED,
            LivenessCheckSessionId = livenessSessionId,
            Confidence = response.Confidence,
            SessionId = sessionId
        };
    }

    /// <summary>
    ///     Verify the liveness of a person for a document. All the images including document, selfie, nose pointing and eyes
    ///     closed should already have been submitted before calling this.
    /// </summary>
    /// <param name="sessionId">The ID of the session to be processed.</param>
    /// <returns></returns>
    /// <exception cref="HttpStatusException"></exception>
    [HttpGet]
    [Route("verify")]
    public async Task<VerifyLivenessResponse> VerifyLiveness(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID must be provided.");

        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        var error = await _livenessChecker.VerifyImageLiveness(sessionId);

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var session = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        var strClient = string.Empty;

        if (HttpContext != null && HttpContext.Request != null &&
            HttpContext.Request.Headers.ContainsKey("User-Agent"))
            strClient = HttpContext.Request.Headers["User-Agent"].ToString();

        var item = new VerificationHistoryItem
        {
            Id = sessionId,
            Client = strClient,
            Timestamp = Convert.ToInt64(DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds),
            IsSuccessful = string.IsNullOrEmpty(error),
            Error = error,
            documentType = session.documentType
        };

        config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.VerificationHistoryTableName
        };

        await _dbContext.SaveAsync(item, config);

        var response = new VerifyLivenessResponse
        {
            IsLive = string.IsNullOrEmpty(error),
            Error = error
        };

        if (!string.IsNullOrEmpty(error))
            await new Notifications().SendVerificationFailureNotification(session, error);

        return response;
    }

    /// <summary>
    ///     The response of a liveness verification request.
    /// </summary>
    public class VerifyLivenessResponse
    {
        /// <summary>
        ///     If true, the document is verified as live.
        /// </summary>
        public bool IsLive { get; set; }

        /// <summary>
        ///     The error message if liveness is false.
        /// </summary>
        public string Error { get; set; }
    }
}