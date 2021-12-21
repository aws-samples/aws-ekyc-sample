using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.CN_Passport
{
    public class PRC_Passport_TextractFieldValueExtractor : IFieldValueExtractor
    {
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private readonly IAmazonTextract _textractClient;

        private List<Block> Blocks;

        public PRC_Passport_TextractFieldValueExtractor(IAmazonTextract textractClient, IConfiguration config,
            ILogger logger)
        {
            _config = config;
            _logger = logger;
            _textractClient = textractClient;
        }

        public async Task<Dictionary<string, string>> GetFieldValues(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("PRC_Passport_TextractFieldValueExtractor::GetFieldValues");

            await LoadBlocks(s3Key);

            var returnValues = new Dictionary<string, string>();

            var fieldNames = new[]
            {
                new Tuple<string, string>("Passport No.", "PassportNo"),
                new Tuple<string, string>("Name", "Name"),
                new Tuple<string, string>("Place of issue", "PlaceOfIssue"),
                new Tuple<string, string>("Place of birth", "PlaceOfBirth"),
                new Tuple<string, string>("Date of birth", "DateOfBirth"),
                new Tuple<string, string>("Nationality", "Nationality"),
                new Tuple<string, string>("Sex", "Sex")
            };

            foreach (var fieldName in fieldNames)
            {
                var block = await FindBlockByText(fieldName.Item1);

                if (block != null)
                {
                    var matchingBlock = await GetNextLineValue(block);

                    if (matchingBlock != null && !string.IsNullOrEmpty(matchingBlock.Text))
                        returnValues[fieldName.Item2] = ProcessString(matchingBlock.Text);
                    else
                        Console.WriteLine($"Cannot find a field value on the CN passport - {fieldName}");
                }
                else
                {
                    Console.WriteLine($"Cannot find a field on the CN passport - {fieldName}");
                }
            }

            AWSXRayRecorder.Instance.EndSubsegment();

            return returnValues;
        }

        private string ProcessString(string text)
        {
            var blockText = text.Trim();

            if (blockText.Trim().StartsWith("/") && blockText.Length > 1)
            {
                blockText = blockText.Substring(1);
                blockText = blockText.Trim();
            }

            var actualText = blockText.Trim('/', ' ');

            return actualText;
        }

        private async Task<Block> FindBlockByText(string text)
        {
            foreach (var block in Blocks)
            {
                var blockText = block.Text;

                var actualText = ProcessString(blockText);

                if (actualText == text)
                    return block;
            }

            return null;
        }

        private async Task<Block> GetNextLineValue(Block blk)
        {
            var matchingBlock = Blocks.Where(a => !string.IsNullOrEmpty(a.Text)
                                                  && a.Geometry.BoundingBox.Top >
                                                  blk.Geometry.BoundingBox.Top + blk.Geometry.BoundingBox.Height
                                                  && a.Geometry.BoundingBox.Left > blk.Geometry.BoundingBox.Left - 0.15
                ).OrderBy(a => a.Geometry.BoundingBox.Top)
                .ThenBy(a => a.Geometry.BoundingBox.Left).FirstOrDefault();

            return matchingBlock;
        }

        private async Task LoadBlocks(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("PRC_Passport_TextractFieldValueExtractor::LoadBlocks");

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

            Blocks = response.Blocks
                .Where(a => a.Confidence > Globals.GetMinimumConfidence() && a.BlockType == BlockType.LINE).ToList();

            //  foreach (var blk in Blocks) Console.WriteLine(blk.Text);

            AWSXRayRecorder.Instance.EndSubsegment();
        }
    }
}