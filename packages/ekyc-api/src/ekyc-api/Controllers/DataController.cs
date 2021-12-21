using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.Controllers
{
    [ApiController]
    [Route("api/data")]
    public class DataController : ControllerBase
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IConfiguration _config;
        private readonly DynamoDBContext _dynamoContext;
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly IDocumentDefinitionFactory _factory;
        private readonly IDocumentChecker _documentChecker;
        private readonly ILogger _logger;

        public DataController(IConfiguration config, IDocumentDefinitionFactory factory, IAmazonS3 amazonS3,
            IAmazonDynamoDB dynamoDb, IDocumentChecker documentChecker, ILogger<DataController> logger)
        {
            _config = config;
            _factory = factory;
            _amazonS3 = amazonS3;
            _logger = logger;
            _dynamoDb = dynamoDb;
            _documentChecker = documentChecker;
            _dynamoContext = new DynamoDBContext(_dynamoDb);
        }

        /// <summary>
        ///     Creates a new data request for getting faces and field data.
        ///     This should be called first before trying to get faces or data from documents.
        /// </summary>
        /// <remarks>Returns the ID of the request to be used</remarks>
        /// <returns></returns>
        [HttpPost]
        [Route("request/create")]
        public async Task<CreateDataRequestResponse> CreateDataRequest()
        {
            var manager = new DataRequestManager(_config, _amazonS3, _dynamoDb);

            string UserAgent = null;

            if (Request != null && Request.Headers.ContainsKey("User-Agent"))
                UserAgent = Request.Headers["User-Agent"].ToString();

            var request = await manager.CreateNewRequest(UserAgent);

            return new CreateDataRequestResponse { RequestId = request.Id };
        }

        /// <summary>
        ///     Gets the faces in a document.
        /// </summary>
        /// <param name="RequestId">The ID of request.</param>
        /// <remarks>Returns the image of the person's face in Base64 format.</remarks>
        /// <exception cref="HttpStatusException"></exception>
        [HttpPost]
        [Route("face")]
        public async Task<GetFacesResponse> GetFaces(S3DataRequest request)
        {
            if (request == null)
                throw new HttpStatusException(HttpStatusCode.BadRequest, "Request cannot be blank.");

            var manager = new DataRequestManager(_config, _amazonS3, _dynamoDb);

            if (request.RequestId == null ||
                !manager.DataRequestExistsAndIsValid(request.RequestId).GetAwaiter().GetResult())
                throw new HttpStatusException(HttpStatusCode.BadRequest,
                    "Request is invalid - did you create a new request first?");

            if (string.IsNullOrEmpty(request.s3Key))
                throw new HttpStatusException(HttpStatusCode.BadRequest, "S3 key must be provided.");

            if (string.IsNullOrEmpty(request.documentType))
                throw new HttpStatusException(HttpStatusCode.BadRequest, "DocumentType cannot be blank.");

            DocumentTypes docType;

            if (!Enum.TryParse(request.documentType, out docType))
                throw new HttpStatusException(HttpStatusCode.BadRequest,
                    $"{request.documentType} is not a valid document type.");

            var documentDefinition = await _factory.GetDocumentDefinition(docType);

            if (!documentDefinition.FaceExtractionSupported)
                throw new HttpStatusException(HttpStatusCode.BadRequest,
                    $"Face extraction is not supported for the document type {request.documentType}.");

            var s3Key = request.RequestId + "/" + request.s3Key;
            var ms = await documentDefinition.GetFace(s3Key);

            var imgBase64 = Convert.ToBase64String(ms.ToArray());

            return new GetFacesResponse { Data = imgBase64 };
        }

        /// <summary>
        /// Gets the field values from a document.
        /// </summary>
        /// <param name="request">The field values detected on the document in a JSON object.</param>
        /// <returns></returns>
        /// <exception cref="HttpStatusException"></exception>
        [HttpPost]
        [Route("fields")]
        public async Task<string> GetFieldValues(S3DataRequest request)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("DataController:GetFieldValues");

            if (request == null)
                throw new HttpStatusException(HttpStatusCode.BadRequest, "Request cannot be blank.");

            var manager = new DataRequestManager(_config, _amazonS3, _dynamoDb);

            if (request.RequestId == null ||
                !manager.DataRequestExistsAndIsValid(request.RequestId).GetAwaiter().GetResult())
                throw new HttpStatusException(HttpStatusCode.BadRequest,
                    "Request is invalid - did you create a new request first?");

            if (string.IsNullOrEmpty(request.s3Key))
                throw new HttpStatusException(HttpStatusCode.BadRequest, "S3Key cannot be blank.");


            DocumentTypes docType;

            var s3Key = request.RequestId + "/" + request.s3Key;

            if (!string.IsNullOrEmpty(request.documentType))
            {
                if (!Enum.TryParse(request.documentType, out docType))
                    throw new HttpStatusException(HttpStatusCode.BadRequest,
                        $"{request.documentType} is not a valid document type.");
            }

            else
            {
                var detectResponse = await new Imaging(_amazonS3, _documentChecker).DetectAndCropDocument(s3Key);

                // The detection may fail because there isn't a Rekognition Custom Labels project running, or it hasn't been trained
                if (detectResponse.HasValue)
                {
                    // We may need to crop out the background by detecting the bounding box of the document

                    // Save the cropped image to S3
                    s3Key = request.RequestId + "/" + Guid.NewGuid().ToString() + ".png";

                    await _amazonS3.PutObjectAsync(new PutObjectRequest()
                    {
                        BucketName = Globals.StorageBucket,
                        AutoCloseStream = true,
                        Key = s3Key,
                        InputStream = detectResponse.Value.CroppedImage
                    });

                    docType = detectResponse.Value.documentType;
                }
                else
                {
                    if (string.IsNullOrEmpty(request.documentType))
                        throw new HttpStatusException(HttpStatusCode.BadRequest,
                            "Unable to infer document type, so DocumentType in the request cannot be blank.");

                    if (!Enum.TryParse(request.documentType, out docType))
                        throw new HttpStatusException(HttpStatusCode.BadRequest,
                            $"{request.documentType} is not a valid document type.");
                }
            }

            var documentDefinition = await _factory.GetDocumentDefinition(docType);

            var fieldData = await documentDefinition.GetFieldData(s3Key, docType);

            AWSXRayRecorder.Instance.EndSubsegment();

            return JsonSerializer.Serialize(fieldData);
        }

        /// <summary>
        /// Returns a presigned URL used for HTTP PUT requests to store assets for data requests.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="s3Key"></param>
        /// <returns>A presigned URL for HTTP PUT operations. The link expires in 15 mins from request.</returns>
        /// <exception cref="HttpStatusException"></exception>
        [HttpGet("url")]
        public async Task<string> GetPresignedUrl(string requestId, string s3Key)
        {
            _logger.LogDebug($"Get Presigned Url: Request Id - {requestId}, S3 Key - {s3Key}");
            var manager = new DataRequestManager(_config, _amazonS3, _dynamoDb);

            if (requestId == null || (!manager.DataRequestExistsAndIsValid(requestId).GetAwaiter().GetResult()))
                throw new HttpStatusException(HttpStatusCode.BadRequest,
                    "Request is invalid - did you create a new request first?");

            s3Key = requestId + "/" + s3Key;

            var presignedUrl = _amazonS3.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = Globals.StorageBucket,
                Key = s3Key,
                Expires = DateTime.UtcNow.AddHours(1),
                Verb = HttpVerb.PUT
            });

            return presignedUrl;
        }

        public class S3DataRequest
        {
            /// <summary>
            ///     The ID of the request to use. To get the request ID, first call /data/request/create.
            /// </summary>
            public string RequestId { get; set; }

            /// <summary>
            ///     The key of the object in S3 that has been uploaded.
            /// </summary>
            public string s3Key { get; set; }

            /// <summary>
            ///     The document type. To make the current supported types, call /document/doctypes
            /// </summary>
            public string documentType { get; set; }
        }
    }
}