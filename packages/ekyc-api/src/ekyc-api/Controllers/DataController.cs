using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Image = Amazon.Rekognition.Model.Image;
using S3Object = Amazon.Rekognition.Model.S3Object;

namespace ekyc_api.Controllers;

[ApiController]
[Route("api/data")]
public class DataController : ControllerBase
{
    private readonly IAmazonS3 _amazonS3;
    private readonly IConfiguration _config;
    private readonly IDocumentChecker _documentChecker;
    private readonly DynamoDBContext _dynamoContext;
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IDocumentDefinitionFactory _factory;
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

    private async Task<GetFacesResponse> GetFaces(S3DataRequest request)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("DataController::GetFaces");
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
        if (ms == null || ms.Length == 0) // No face detected
            return new GetFacesResponse { Data = null };
        var imgBase64 = Convert.ToBase64String(ms.ToArray());

        AWSXRayRecorder.Instance.EndSubsegment();

        return new GetFacesResponse { Data = imgBase64 };
    }


    /// <summary>
    ///     Gets the faces in a document.
    /// </summary>
    /// <param name="RequestId">The ID of request.</param>
    /// <remarks>Returns the image of the person's face in Base64 format.</remarks>
    /// <exception cref="HttpStatusException"></exception>
    [HttpPost]
    [Route("face")]
    public async Task<GetFacesResponse> GetFacesApi(S3DataRequest request)
    {
        if (request == null)
            throw new HttpStatusException(HttpStatusCode.BadRequest, "Request cannot be blank.");

        return await GetFaces(request);
    }

    private async Task<GetLandmarksResponse> GetLandmarks(S3DataRequest request)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("DataController:GetLandmarks");

        _logger.LogInformation($"Get landmarks for S3 key {request.s3Key}");
        var rekognition = new AmazonRekognitionClient();
        var s3 = new AmazonS3Client();
        var s3Key = request.RequestId + "/" + request.s3Key;
        var detectResponse = await rekognition.DetectCustomLabelsAsync(new DetectCustomLabelsRequest
        {
            Image = new Image
            {
                S3Object = new S3Object
                {
                    Bucket = Globals.StorageBucket,
                    Name = s3Key
                }
            },
            ProjectVersionArn = Globals.ThaiIdRekognitionCustomLabelsProjectArn
        });

        // Get the image from S3
        var getImageResponse = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = Globals.StorageBucket,
            Key = s3Key
        });

        var documentImage = SixLabors.ImageSharp.Image.Load(getImageResponse.ResponseStream);
        Pen pen = Pens.Solid(Color.Red, 5);

        foreach (var label in detectResponse.CustomLabels.Where(a => a.Confidence > 30))
        {
            var points = new[]
            {
                new PointF(label.Geometry.BoundingBox.Left * Convert.ToSingle(documentImage.Size.Width),
                    label.Geometry.BoundingBox.Top * Convert.ToSingle(documentImage.Size.Height)),
                new PointF(
                    (label.Geometry.BoundingBox.Width + label.Geometry.BoundingBox.Left) *
                    Convert.ToSingle(documentImage.Size.Width),
                    label.Geometry.BoundingBox.Top * Convert.ToSingle(documentImage.Size.Height)),
                new PointF(
                    (label.Geometry.BoundingBox.Width + label.Geometry.BoundingBox.Left) *
                    Convert.ToSingle(documentImage.Size.Width),
                    (label.Geometry.BoundingBox.Top + label.Geometry.BoundingBox.Height) *
                    Convert.ToSingle(documentImage.Size.Height)),
                new PointF(label.Geometry.BoundingBox.Left * Convert.ToSingle(documentImage.Size.Width),
                    (label.Geometry.BoundingBox.Top + label.Geometry.BoundingBox.Height) *
                    Convert.ToSingle(documentImage.Size.Height))
            };

            documentImage.Mutate(x => x.DrawPolygon(pen, points));
        }

        var msOutput = new MemoryStream();
        documentImage.SaveAsPng(msOutput);

        var response = new GetLandmarksResponse();
        response.Data = Convert.ToBase64String(msOutput.ToArray());
        AWSXRayRecorder.Instance.EndSubsegment();
        return response;
    }

    [HttpPost]
    [Route("landmarks")]
    public async Task<GetLandmarksResponse> GetLandmarksApi(S3DataRequest request)
    {
        if (request == null)
            throw new HttpStatusException(HttpStatusCode.BadRequest, "Request cannot be blank.");
        return await GetLandmarks(request);
    }

    /// <summary>
    ///     Gets all the data from a document, including field values, landmarks and faces.
    /// </summary>
    /// <param name="request">The field values detected on the document in a JSON object.</param>
    /// <returns>The field values, faces and landmarks detected on the document in a JSON object</returns>
    /// <exception cref="HttpStatusException"></exception>
    [HttpPost]
    [Route("fields/full")]
    public async Task<GetFullDocumentResponse> GetAllDocumentData(S3DataRequest request)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("DataController:GetAllDocumentData");
        try
        {
            DocumentTypes docType;

            if (!Enum.TryParse(request.documentType, out docType))
                throw new HttpStatusException(HttpStatusCode.BadRequest,
                    $"{request.documentType} is not a valid document type.");

            var documentDefinition = await _factory.GetDocumentDefinition(docType);

            Task<GetFacesResponse> faceTask = null;
            Task<GetFieldValuesResponse> fieldValuesTask = null;
            Task<GetLandmarksResponse> landmarksTask = null;
            List<Task> tasks = new();
            fieldValuesTask = Task.Run(async () => await GetFieldValues(request));
            tasks.Add(fieldValuesTask);

            if (documentDefinition.FaceExtractionSupported)
            {
                faceTask = Task.Run(async () => await GetFaces(request));
                tasks.Add(faceTask);
            }

            if (documentDefinition.Landmarks?.Length > 0)
            {
                landmarksTask = Task.Run(async () => await GetLandmarks(request));
                tasks.Add(landmarksTask);
            }

            Task.WaitAll(tasks.ToArray());

            var response = new GetFullDocumentResponse
            {
                FieldValues = fieldValuesTask.Result
            };

            if (documentDefinition.FaceExtractionSupported)
                response.Faces = faceTask?.Result;
            if (documentDefinition.Landmarks?.Length > 0)
                response.Landmarks = landmarksTask?.Result;


            return response;
        }
        catch
        {
            AWSXRayRecorder.Instance.EndSubsegment();
            throw;
        }
    }

    private async Task<GetFieldValuesResponse> GetFieldValues(S3DataRequest request)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("DataController:GetFieldValues");

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
                s3Key = request.RequestId + "/" + Guid.NewGuid() + ".png";

                await _amazonS3.PutObjectAsync(new PutObjectRequest
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

        return new GetFieldValuesResponse { FieldValues = fieldData };
    }

    /// <summary>
    ///     Gets the field values from a document.
    /// </summary>
    /// <param name="request">The document to be assessed.</param>
    /// <returns>The field values detected on the document in a JSON object.</returns>
    /// <exception cref="HttpStatusException"></exception>
    [HttpPost]
    [Route("fields")]
    public async Task<GetFieldValuesResponse> GetFieldValuesApi(S3DataRequest request)
    {
        if (request == null)
            throw new HttpStatusException(HttpStatusCode.BadRequest, "Request cannot be blank.");

        return await GetFieldValues(request);
    }

    /// <summary>
    ///     Returns a presigned URL used for HTTP PUT requests to store assets for data requests.
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

        if (requestId == null || !manager.DataRequestExistsAndIsValid(requestId).GetAwaiter().GetResult())
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