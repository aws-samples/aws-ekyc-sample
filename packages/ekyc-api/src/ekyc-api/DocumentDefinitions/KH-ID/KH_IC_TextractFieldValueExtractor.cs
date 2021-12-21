using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.KH_ID
{
    public class KH_IC_TextractFieldValueExtractor : IFieldValueExtractor
    {
        private readonly IConfiguration _config;
        private readonly ILogger _logger;

        private readonly IAmazonTextract _textractClient;

        private List<Block> Blocks;

        public KH_IC_TextractFieldValueExtractor(IAmazonTextract textractClient, IConfiguration config,
            ILogger logger)
        {
            _config = config;
            _logger = logger;
            _textractClient = textractClient;
        }

        public async Task<Dictionary<string, string>> GetFieldValues(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("KH_IC_TextractFieldValueExtractor::GetFieldValues");

            await LoadBlocks(s3Key);

            var regex = new Regex(@"^\d{9}$");

            var idBlock = Blocks.FirstOrDefault(a =>
                a.Confidence > Globals.GetMinimumConfidence() && !string.IsNullOrEmpty(a.Text) &&
                regex.IsMatch(a.Text));

            if (idBlock == null)
                throw new Exception("Unable to find ID on card.");

            var dict = new Dictionary<string, string>();

            dict["Id"] = idBlock.Text;

            regex = new Regex(@"^[a-zA-Z\s]+$");

            var nameBlock = Blocks.FirstOrDefault(a => a.Confidence > Globals.GetMinimumConfidence()
                                                       && a.Geometry.BoundingBox.Top < 0.5f
                                                       && !string.IsNullOrEmpty(a.Text)
                                                       && regex.IsMatch(a.Text));

            if (nameBlock == null)
                throw new Exception("Unable to find name on card.");

            dict["Name"] = nameBlock.Text;

            AWSXRayRecorder.Instance.EndSubsegment();

            return dict;
        }

        private async Task LoadBlocks(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("KH_IC_TextractFieldValueExtractor::LoadBlocks");

            var response = await _textractClient.AnalyzeDocumentAsync(new AnalyzeDocumentRequest
                {
                    Document = new Document
                    {
                        S3Object = new S3Object
                        {
                            Bucket = Globals.StorageBucket,
                            Name = s3Key
                        }
                    },
                    FeatureTypes = new List<string> { "FORMS" }
                }
            );

            Blocks = response.Blocks;

            AWSXRayRecorder.Instance.EndSubsegment();
        }
    }
}