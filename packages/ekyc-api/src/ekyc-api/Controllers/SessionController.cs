using System;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.S3.Model;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace ekyc_api.Controllers;

[ApiController]
[Route("api/session")]
public class SessionController : ControllerBase
{
    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IAmazonS3 _amazonS3;

    private readonly IConfiguration _configuration;

    private readonly DynamoDBContext _dbContext;

    private readonly IDocumentChecker _documentChecker;

    private readonly ILivenessChecker _livenessChecker;

    private readonly ILogger<SessionController> _logger;

    private readonly SessionManager _sessionManager;

    public SessionController(IConfiguration config, IAmazonS3 amazonS3, IAmazonDynamoDB amazonDynamoDb,
        ILivenessChecker livenessChecker, IDocumentChecker documentChecker, ILogger<SessionController> logger)
    {
        _configuration = config;
        _livenessChecker = livenessChecker;
        _amazonS3 = amazonS3;
        _amazonDynamoDb = amazonDynamoDb;
        _dbContext = new DynamoDBContext(_amazonDynamoDb);
        _sessionManager = new SessionManager(config, amazonS3, amazonDynamoDb);
        _documentChecker = documentChecker;
        _logger = logger;
    }

    /// <summary>
    ///     Starts a new session for liveness checking.
    /// </summary>
    /// <returns>
    ///     The session ID for using across multiple liveness check requests, and the coordinates for the nose point
    ///     rectangle that the user is expected to point his nose into.
    /// </returns>
    [HttpPost]
    [Route("new")]
    public async Task<NewSessionResponse> StartNewSession()
    {
        string UserAgent = null;

        if (Request != null && Request.Headers.ContainsKey("User-Agent"))
            UserAgent = Request.Headers["User-Agent"].ToString();

        var sessionResponse = await _sessionManager.CreateNewSession(UserAgent);

        return new NewSessionResponse
        {
            Id = sessionResponse.Id,
            noseBoundsHeight = Globals.NosePointAreaDimensions,
            noseBoundsWidth = Globals.NosePointAreaDimensions,
            noseBoundsLeft = sessionResponse.nosePointAreaLeft.GetValueOrDefault(0f),
            noseBoundsTop = sessionResponse.nosePointAreaTop.GetValueOrDefault(0f)
        };
    }

    [HttpGet]
    [Route("image/url/{sessionId}")]
    public async Task<GetImageUrlsForSessionResponse> GetImageUrls(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID cannot be blank.");


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

        if (string.IsNullOrEmpty(session.documentImageKey))
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                "The document image is not found - have you done a POST to /document?");

        if (string.IsNullOrEmpty(session.selfieImageKey))
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                "The selfie is not found - have you done a POST to /selfie?");

        var response = new GetImageUrlsForSessionResponse { SessionId = sessionId };

        response.DocumentUrl = _amazonS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Globals.StorageBucket,
            Key = session.documentImageKey,
            Expires = DateTime.Now.AddHours(1)
        });
        response.SelfieUrl = _amazonS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Globals.StorageBucket,
            Key = session.selfieImageKey,
            Expires = DateTime.Now.AddHours(1)
        });

        return response;
    }


    /// <summary>
    ///     Gets a presigned URL to allow uploads using the HTTP PUT verb.
    /// </summary>
    /// <param name="sessionId">The session ID that is being used across multiple requests.</param>
    /// <returns>The presigned URL for uploading to the specified S3 key.</returns>
    /// <exception cref="HttpStatusException"></exception>
    [HttpGet]
    [Route("url")]
    public async Task<string> GetPresignedPutUrl(string sessionId, string s3Key)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID cannot be blank.");

        if (string.IsNullOrEmpty(s3Key))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "S3 key cannot be blank.");

        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        s3Key = sessionId + "/" + s3Key;

        var presignedUrl = _amazonS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Globals.StorageBucket,
            Key = s3Key,
            Expires = DateTime.UtcNow.AddMinutes(15),
            Verb = HttpVerb.PUT
        });

        return presignedUrl;
    }

    [HttpPost]
    [Route("compare")]
    public async Task<CompareDocumentWithSelfie> CompareDocumentWithSelfie(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID cannot be blank.");


        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var item = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (item == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        if (string.IsNullOrEmpty(item.documentImageKey))
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                "The document image is not found - have you done a POST to /document?");

        if (string.IsNullOrEmpty(item.selfieImageKey))
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                "The selfie is not found - have you done a POST to /selfie?");

        var result = _livenessChecker.CompareFaces(item.selfieImageKey, item.documentImageKey).GetAwaiter()
            .GetResult();

        return new CompareDocumentWithSelfie { IsSimilar = result.IsMatch, Similarity = result.Confidence };
    }

    /// <summary>
    ///     Submit a selfie image for liveness checking.
    /// </summary>
    /// <param name="sessionId">The session ID that is being used across multiple requests.</param>
    /// <param name="s3Key">The key of the selfie stored in S3. Must be a valid image of a person with eyes open.</param>
    /// <exception cref="HttpStatusException"></exception>
    [HttpPost]
    [Route("selfie")]
    public async Task SubmitSelfie(string sessionId, string s3Key)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID cannot be blank.");

        if (string.IsNullOrEmpty(s3Key))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "S3 key cannot be blank.");

        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var item = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (item == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        var imaging = new Imaging(_amazonS3, _documentChecker);
        Image img;

        s3Key = sessionId + "/" + s3Key;

        _logger.LogDebug($"Using {s3Key} as selfie.");

        try
        {
            img = await imaging.GetImageFromStorage(s3Key);
        }
        catch (ImageFormatException)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Key provided is not a valid image.");
        }
        catch (Exception ex)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, ex.Message);
        }

        if (img == null)
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Image is not valid.");

        await _livenessChecker.VerifyImageSize(img);

        var eyesOpen = await _livenessChecker.VerifyEyesOpen(s3Key);

        if (!eyesOpen)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                "Please make sure your eyes are open.");

        var verification = await _livenessChecker.VerifySelfieFacePose(s3Key);

        if (!string.IsNullOrEmpty(verification))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, verification);

        var faceIsInCentre = await _livenessChecker.VerifyFaceIsInCentre(s3Key);

        if (!faceIsInCentre)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                "Please ensure your face is in the centre of the image.");

        item.selfieImageKey = s3Key;

        await _dbContext.SaveAsync(item, config);
    }

    /// <summary>
    ///     Submits a document for liveness verification.
    /// </summary>
    /// <param name="sessionId">The session ID that is being used across multiple requests.</param>
    /// <param name="s3Key">The s3 key where the document has been uploaded.</param>
    /// <param name="expectedDocumentType">The type of document that has been uploaded.</param>
    /// <exception cref="HttpStatusException"></exception>
    /// <exception cref="Exception"></exception>
    [HttpPost]
    [Route("document")]
    public async Task SubmitDocumentForVerification(string sessionId, string s3Key, string expectedDocumentType)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID cannot be blank.");

        if (string.IsNullOrEmpty(s3Key))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "S3 key cannot be blank.");

        if (string.IsNullOrEmpty(expectedDocumentType))
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                "Expected document type cannot be blank.");

        // Check that the document type is supported
        DocumentTypes docType;

        if (!Enum.TryParse(expectedDocumentType, true, out docType))
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Document type {expectedDocumentType} is not supported.");

        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        s3Key = sessionId + "/" + s3Key;


        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var item = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (item == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        var imaging = new Imaging(_amazonS3, _documentChecker);

        Image img;

        // Validate the document type

        try
        {
            img = await imaging.GetImageFromStorage(s3Key);
        }
        catch (ImageFormatException)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Key provided is not a valid image.");
        }
        catch (Exception ex)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, ex.Message);
        }

        if (img == null)
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Image is not valid.");

        await _livenessChecker.VerifyImageSize(img);

        item.documentImageKey = s3Key;

        item.documentType = docType.ToString();

        await _dbContext.SaveAsync(item, config);
    }

    /// <summary>
    ///     Submits a nose pointing image for liveness verification.
    /// </summary>
    /// <param name="sessionId">The session ID that is being used across multiple requests.</param>
    /// <param name="s3Key">The key of the image stored in S3.</param>
    /// <exception cref="HttpStatusException"></exception>
    [HttpPost]
    [Route("nosepoint")]
    public async Task SubmitFaceWithNosePointing(string sessionId, string s3Key)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID cannot be blank.");

        if (string.IsNullOrEmpty(s3Key))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "S3 key cannot be blank.");

        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var item = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (item == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        var imaging = new Imaging(_amazonS3, _documentChecker);

        Image img;

        s3Key = sessionId + "/" + s3Key;

        try
        {
            img = await imaging.GetImageFromStorage(s3Key);
        }
        catch (ImageFormatException)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Key provided is not a valid image.");
        }
        catch (Exception ex)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, ex.Message);
        }

        if (img == null)
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Image is not valid.");

        await _livenessChecker.VerifyImageSize(img);

        item.nosePointImageKey = s3Key;

        await _dbContext.SaveAsync(item, config);
    }

    /// <summary>
    ///     Submits the eyes closed image for liveness verification.
    /// </summary>
    /// <param name="sessionId">The session ID that is being used across multiple requests.</param>
    /// <param name="s3Key">The key of the image stored in S3.</param>
    /// <exception cref="HttpStatusException"></exception>
    [HttpPost]
    [Route("eyesclosed")]
    public async Task SubmitEyesClosedFace(string sessionId, string s3Key)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session ID cannot be blank.");

        if (string.IsNullOrEmpty(s3Key))
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "S3 key cannot be blank.");

        // Check if the session exists
        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid)
            throw new HttpStatusException(HttpStatusCode.InternalServerError,
                $"Session ID {sessionId} not found or has expired.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var item = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (item == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        s3Key = sessionId + "/" + s3Key;

        var imaging = new Imaging(_amazonS3, _documentChecker);

        Image img;

        try
        {
            img = await imaging.GetImageFromStorage(s3Key);
        }
        catch (ImageFormatException)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Key provided is not a valid image.");
        }
        catch (Exception ex)
        {
            throw new HttpStatusException(HttpStatusCode.InternalServerError, ex.Message);
        }

        if (img == null)
            throw new HttpStatusException(HttpStatusCode.InternalServerError, "Image is not valid.");

        await _livenessChecker.VerifyEyesOpen(s3Key);

        item.eyesClosedImageKey = s3Key;

        await _dbContext.SaveAsync(item, config);
    }

    public class NewSessionResponse
    {
        /// <summary>
        ///     The ID of the session to use across multiple liveness check calls.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     The top of the nose-pointing rectangle in percent compared to the document's height.
        /// </summary>
        public double noseBoundsTop { get; set; }

        /// <summary>
        ///     The left of the nose-pointing rectangle in percent compared to the document's width.
        /// </summary>
        public double noseBoundsLeft { get; set; }

        /// <summary>
        ///     The width of the nose-pointing rectangle in percent compared to the document's width.
        /// </summary>
        public double noseBoundsWidth { get; set; }

        /// <summary>
        ///     The height of the nose-pointing rectangle in percent compared to the document's height.
        /// </summary>
        public double noseBoundsHeight { get; set; }
    }
}