using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
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
    [Route("api/history")]
    public class VerificationHistoryController : Controller
    {
        private readonly IAmazonDynamoDB _amazonDynamoDb;

        private readonly ILogger<VerificationHistoryController> _logger;

        private readonly IConfiguration _config;

        private readonly DynamoDBContext _dbContext;

        public VerificationHistoryController(IConfiguration config, IDocumentDefinitionFactory factory,
            IAmazonS3 amazonS3,
            IAmazonDynamoDB amazonDynamoDb,
            IDocumentChecker documentChecker, ILogger<VerificationHistoryController> logger)
        {
            this._config = config;

            _amazonDynamoDb = amazonDynamoDb;

            _logger = logger;

            _dbContext = new DynamoDBContext(_amazonDynamoDb);
        }

        [HttpGet]
        public async Task<HistoryItems[]> GetHistoryItems()
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = Globals.VerificationHistoryTableName
            };

            var conditions = new ScanCondition[] { };

            var items = await _dbContext.ScanAsync<VerificationHistoryItem>(conditions, config).GetRemainingAsync();

            var returnItems = items.Select(a => new HistoryItems()
            {
                SessionId = a.Id,
                Error = a.Error,
                Time = DateTime.UnixEpoch.Add(TimeSpan.FromSeconds(a.Timestamp)),
                DocumentType = a.documentType,
                IsSuccessful = a.IsSuccessful,
                Client = a.Client
            }).ToList();

            returnItems = returnItems.OrderByDescending(a => a.Time).ToList();

            return returnItems.ToArray();
        }
    }
}