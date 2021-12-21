using System;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ekyc_api.Controllers
{
    [ApiController]
    [Route("api/liveness")]
    public class LivenessController : ControllerBase
    {
        private readonly IAmazonDynamoDB _amazonDynamoDb;

        private readonly IAmazonS3 _amazonS3;

        private readonly IConfiguration _config;

        private readonly DynamoDBContext _dbContext;

        private readonly ILivenessChecker _livenessChecker;

        private readonly SessionManager _sessionManager;

        public LivenessController(IConfiguration config, IAmazonS3 amazonS3, IAmazonDynamoDB amazonDynamoDb,
            ILivenessChecker livenessChecker)
        {
            _config = config;
            _livenessChecker = livenessChecker;
            _amazonS3 = amazonS3;
            _amazonDynamoDb = amazonDynamoDb;
            _dbContext = new DynamoDBContext(_amazonDynamoDb);
            _sessionManager = new SessionManager(_config, _amazonS3, _amazonDynamoDb);
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
}