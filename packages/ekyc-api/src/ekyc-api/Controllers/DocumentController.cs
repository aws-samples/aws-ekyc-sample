using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.Controllers;

[ApiController]
[Route("api/document")]
public class DocumentController : ControllerBase
{
    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IAmazonS3 _amazonS3;

    private readonly DynamoDBContext _dbContext;

    private readonly IDocumentChecker _documentChecker;

    private readonly IDocumentDefinitionFactory _documentDefinitionFactory;

    private readonly ILogger<DocumentController> _logger;

    private readonly SessionManager _sessionManager;

    private readonly IConfiguration config;

    public DocumentController(IConfiguration config, IDocumentDefinitionFactory factory, IAmazonS3 amazonS3,
        IAmazonDynamoDB amazonDynamoDb,
        IDocumentChecker documentChecker, ILogger<DocumentController> logger)
    {
        this.config = config;
        _amazonS3 = amazonS3;
        _documentDefinitionFactory = factory;
        _amazonDynamoDb = amazonDynamoDb;
        _sessionManager = new SessionManager(config, amazonS3, amazonDynamoDb);
        _documentChecker = documentChecker;
        _dbContext = new DynamoDBContext(_amazonDynamoDb);
        _logger = logger;
    }

    /// <summary>
    ///     Gets the document types that are currently supported.
    /// </summary>
    /// <returns>List of document types with names and codes.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DocumentDefinitionSummary>), StatusCodes.Status200OK)]
    [Route("doctypes")]
    public async Task<DocumentDefinitionSummary[]> GetSupportedDocTypes()
    {
        var lstReturn = new List<DocumentDefinitionSummary>();

        var enumValues = Enum.GetValues(typeof(DocumentTypes));

        foreach (var docType in enumValues)
        {
            var documentDefinition = await _documentDefinitionFactory.GetDocumentDefinition((DocumentTypes)docType);
            if (documentDefinition != null)
            {
                var item = new DocumentDefinitionSummary();
                item.Name = documentDefinition.Name;
                item.Code = docType.ToString();
                item.FaceExtractionSupported = documentDefinition.FaceExtractionSupported;
                item.LivenessSupported = documentDefinition.LivenessSupported;

                lstReturn.Add(item);
            }
            else
            {
                _logger.LogWarning($"Document type {docType} does not have a document definition.");
            }
        }

        return lstReturn.OrderBy(a => a.Name).ToArray();
    }


    /// <summary>
    ///     Tries to detect the type of document from an image.
    /// </summary>
    /// <param name="s3Key">The key of the object stored in S3. This should be an image.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    [HttpPost]
    [Route("type")]
    public async Task<string> GetDocumentType(string s3Key)
    {
        if (string.IsNullOrEmpty(s3Key))
            throw new ArgumentException("S3 key cannot be blank.");

        var documentTypeResponse = await _documentChecker.GetDocumentType(s3Key);

        if (documentTypeResponse == null || documentTypeResponse.Type == null)
            throw new Exception("No supported document types found for this image.");

        return documentTypeResponse.Type.Value.ToString();
    }

    /// <summary>
    ///     Detects the document type and sets the session document type.
    /// </summary>
    /// <param name="sessionId">The ID of the session that this document belongs to.</param>
    /// <param name="s3Key">The key of the document that is stored in S3. This should be an image.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    /// <exception cref="HttpStatusException"></exception>
    [HttpPost]
    [Route("set")]
    public async Task<string> DetectAndSetDocumentType(string sessionId, string s3Key)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session Id cannot be blank.");

        if (string.IsNullOrEmpty(s3Key))
            throw new ArgumentException("S3 key cannot be blank.");

        var sessionValid = await _sessionManager.SessionExistsAndIsValid(sessionId);

        if (!sessionValid) throw new ArgumentException($"Session ID {sessionId} does not exist or has expired.");

        s3Key = sessionId + "/" + s3Key;

        var documentTypeResponse = await _documentChecker.GetDocumentType(s3Key);

        if (documentTypeResponse == null || documentTypeResponse.Type == null)
            throw new Exception("No supported document types found for this image.");

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var item = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        if (item == null) throw new HttpStatusException(HttpStatusCode.InternalServerError, "Session not found.");

        item.documentType = documentTypeResponse.Type.ToString();

        item.documentBoundingBox = JsonSerializer.Serialize(documentTypeResponse.BoundingBox);

        await _dbContext.SaveAsync(item, config);

        return item.documentType;
    }
}