using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.XRay.Recorder.Core;
using CsvHelper;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions
{
    public class MYKad_TextractFieldValueExtractor : IFieldValueExtractor
    {
        private const double HeaderLeftThreshold = 0.25d; // The maximum threshold that a header left corner will be

        private const double
            FieldLeftDiffThreshold =
                0.05d; // The threshold for differences in the left coordinate of boxes that constitute the same field

        private readonly IConfiguration _config;
        private readonly ILogger _logger;

        private readonly IAmazonTextract _textractClient;

        private List<Block> Blocks;

        public MYKad_TextractFieldValueExtractor(IAmazonTextract textractClient, IConfiguration config,
            ILogger logger)
        {
            _config = config;
            _logger = logger;
            _textractClient = textractClient;
        }

        public async Task<Dictionary<string, string>> GetFieldValues(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("MYKad_TextractFieldValueExtractor::GetFieldValues");

            if (!File.Exists("states.json"))
                throw new Exception("Malaysian states file not defined.");

            List<MalaysianState> states = null;

            using (var sr = File.OpenText("states.json"))
            {
                states = JsonSerializer.Deserialize<List<MalaysianState>>(sr.ReadToEnd());
            }


            if (Blocks == null) await LoadBlocks(s3Key);


            var sortedBlocks = Blocks
                .OrderBy(x => x.Geometry.BoundingBox.Top)
                .ThenBy(x => x.Geometry.BoundingBox.Left)
                .ToList();

            var lines = sortedBlocks
                .Where(b => b.BlockType == BlockType.LINE)
                .OrderBy(x => x.Geometry.BoundingBox.Top)
                .ThenBy(x => x.Geometry.BoundingBox.Left)
                .ToList();


            foreach (var line in lines)
                _logger.LogDebug(
                    $"{line.Text} - X: {line.Geometry.BoundingBox.Left} Y: {line.Geometry.BoundingBox.Top} Width: {line.Geometry.BoundingBox.Width} Height: {line.Geometry.BoundingBox.Height}");

            var values = new Dictionary<string, string>();


            var topLeftKadBlock = GetTopLeftKadBlock(lines);

            if (topLeftKadBlock == null)
            {
                AWSXRayRecorder.Instance.EndSubsegment();
                throw new Exception("Unable to find myKad landmark - Kad Pengenalan");
            }

            // Get the state line

            var stateLine = lines.FirstOrDefault(x =>
                states.Any(s => s.name.ToLower().Trim() == x.Text.Replace(" ", "").ToLower().Trim()));

            if (stateLine == null)
            {
                var ex = new Exception("Unable to find the user's state on this IC.");
                AWSXRayRecorder.Instance.AddException(ex);

                AWSXRayRecorder.Instance.EndSubsegment();
                throw ex;
            }

            /* if (!CheckDocumentPerspective(s3Key, topLeftKadBlock).GetAwaiter().GetResult())
             {
                 AWSXRayRecorder.Instance.EndSubsegment();
                 throw new Exception("The document's perspective is incorrect. Please take a photo of the document directly above it so that the edges are squared.");
             }
             */


            var regexNRIC = new Regex(@"^\d{6}-\d{2}-?(?:\d{4})?$", RegexOptions.Compiled);

            var nricLine = lines.FirstOrDefault(x =>
                x.Geometry.BoundingBox.Top - topLeftKadBlock.Geometry.BoundingBox.Top <= 0.3
                && x.Geometry.BoundingBox.Left <= topLeftKadBlock.Geometry.BoundingBox.Left
                && x.Geometry.BoundingBox.Left + x.Geometry.BoundingBox.Width <
                topLeftKadBlock.Geometry.BoundingBox.Left +
                topLeftKadBlock.Geometry.BoundingBox.Width
                && regexNRIC.IsMatch(x.Text));

            if (nricLine != null)
            {
                if (!string.IsNullOrEmpty(nricLine.Text))
                    values["NRIC"] = nricLine.Text;
            }
            else
            {
                var ex = new Exception("Unable to find the NRIC number on this IC.");
                AWSXRayRecorder.Instance.AddException(ex);

                AWSXRayRecorder.Instance.EndSubsegment();
                throw ex;
            }


            // Get the height multiplier for this image - it's how much height the NRIC takes up as a percentage of the whole image

            var absoluteTop = stateLine.Geometry.BoundingBox.Top + stateLine.Geometry.BoundingBox.Height;

            var heightMultiplier = absoluteTop - nricLine.Geometry.BoundingBox.Top;


            // Try to get the name and address lines

            var nameAddressLines = lines.Where(a =>
                    // The left of the line must be within 2% threshold of the NRIC line
                    Math.Abs(a.Geometry.BoundingBox.Left - nricLine.Geometry.BoundingBox.Left) < 0.03
                    // After checking for the absolute position of the card, the line must be higher than 30% of the card's height
                    && a.Geometry.BoundingBox.Top - topLeftKadBlock.Geometry.BoundingBox.Top > 0.3
                )
                .OrderBy(a => a.Geometry.BoundingBox.Top)
                .ThenBy(a => a.Geometry.BoundingBox.Left)
                .ToList();

            // Get the name first

            var strName = "";
            var nameaddressLineThreshold = 0.05f * heightMultiplier;
            var currentMaxTop = 0f;
            var nameLines = 0;

            foreach (var line in nameAddressLines)
            {
                nameLines++;
                currentMaxTop = line.Geometry.BoundingBox.Top;
                if (string.IsNullOrEmpty(strName))
                    strName += line.Text;
                else
                    strName += " " + line.Text;

                // Check if there's another line immediately below
                var hasLine = nameAddressLines.Any(a => a.Id != line.Id
                                                        && Math.Abs(a.Geometry.BoundingBox.Top -
                                                                    (line.Geometry.BoundingBox.Top +
                                                                     line.Geometry.BoundingBox.Height)) <
                                                        nameaddressLineThreshold
                );
                if (!hasLine || nameLines >= 2) // Max of 2 lines for the name
                    break;
            }

            values["Name"] = strName;

            List<PostcodeCsvRow> postcodeData = null;

            using (var reader = new StreamReader("./DocumentDefinitions/MY-NRIC/postcodes.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                postcodeData = csv.GetRecords<PostcodeCsvRow>().ToList();
            }

            var addressLines = nameAddressLines.Where(a =>
                    !string.IsNullOrEmpty(a.Text)
                    && a.Geometry.BoundingBox.Top > currentMaxTop
                    && a.Geometry.BoundingBox.Top <= stateLine.Geometry.BoundingBox.Top
                )
                .OrderBy(a => a.Geometry.BoundingBox.Top)
                .ThenBy(a => a.Geometry.BoundingBox.Left)
                .ToList();


            var regexPostcode = new Regex(@"^(?<postcode>\d{4,5})\s(?<area>.+)$", RegexOptions.Compiled);
            for (var i = 0; i < addressLines.Count; i++)
                if (regexPostcode.IsMatch(addressLines[i].Text))
                {
                    var matches = regexPostcode.Matches(addressLines[i].Text);
                    if (matches.Count > 0)
                    {
                        values["AddressPostCode"] = matches[0].Groups["postcode"].Value;
                        // Look up the area via CSV
                        if (postcodeData.Any(a => a.PostCode == values["AddressPostCode"]))
                        {
                            var postcodeRow = postcodeData.First(a => a.PostCode == values["AddressPostCode"]);
                            values["AddressCity"] = postcodeRow.City;
                            values["AddressState"] = postcodeRow.State;
                        }
                        else
                        {
                            values["AddressCity"] = matches[0].Groups["area"].Value;
                        }
                    }
                    else
                    {
                        values["AddressLine_" + (i + 1)] = addressLines[i].Text;
                    }
                }
                else
                {
                    values["Addressline_" + (i + 1)] = addressLines[i].Text;
                }


            // Try to get the gender/religion line
            var male = "lelaki";
            var female = "perempuan";
            var genderLine = lines.FirstOrDefault(a =>
                a.Text.ToLower().EndsWith(female) || a.Text.ToLower().EndsWith(male));
            if (genderLine != null)
            {
                var splitGenderLine = genderLine.Text.Trim().Split(" ");

                if (splitGenderLine.Length > 1) // Religion also exists
                {
                    values["Gender"] = splitGenderLine[1].Trim().ToLower() == male ? "M" : "F";
                    values["Religion"] = splitGenderLine[0].Trim();
                }
                else
                {
                    values["Gender"] = splitGenderLine[0].Trim().ToLower() == male ? "M" : "F";

                    var religionLine = lines.FirstOrDefault(a => a.Id != genderLine.Id
                                                                 && Math.Abs(a.Geometry.BoundingBox.Top -
                                                                             genderLine.Geometry.BoundingBox.Top) < 0.01
                                                                 && a.Geometry.BoundingBox.Left > 0.6
                                                                 && !string.IsNullOrEmpty(a.Text)
                    );
                    if (religionLine != null)
                        values["Religion"] = religionLine.Text.Trim();
                }
            }


            var strDict = JsonSerializer.Serialize(values);

            Console.WriteLine($"MyKAD OCR Fields: {strDict}");

            AWSXRayRecorder.Instance.EndSubsegment();

            return values;
        }

        private async Task LoadBlocks(string s3Key)
        {
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
        }

        /*       private async Task<bool> CheckDocumentPerspective(string s3Key,Block topLeftKadBlock)
               {
                   AWSXRayRecorder.Instance.BeginSubsegment("MYKad_TextractFieldValueExtractor::CheckDocumentPerspective");
                   
                   if (Blocks == null)
                   {
                       await LoadBlocks(s3Key);
                   }
                   
                   var sortedBlocks = Blocks
                       .OrderBy(x => x.Geometry.BoundingBox.Top)
                       .ThenBy(x => x.Geometry.BoundingBox.Left)
                       .ToList();
       
                   var lines = sortedBlocks
                       .Where(b => b.BlockType == BlockType.LINE)
                       .OrderBy(x => x.Geometry.BoundingBox.Top)
                       .ThenBy(x => x.Geometry.BoundingBox.Left)
                       .ToList();
       
                 
                   // Make sure there are lines further down the document matching the horizontal position
       
                   var linesBelow = lines.Where(a => a.Id != topLeftKadBlock.Id &&
                                    Math.Abs(a.Geometry.BoundingBox.Left - topLeftKadBlock.Geometry.BoundingBox.Left) < 0.02);
       
                   AWSXRayRecorder.Instance.EndSubsegment();
                   
                   return linesBelow.Any();
               }
       */

        /// <summary>
        ///     Tries to get the top left "MyKad" block to be used as an anchor for all other text identification.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        private Block GetTopLeftKadBlock(List<Block> lines)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("MYKad_TextractFieldValueExtractor::GetTopLeftKadBlock");

            string[] strValues = { "kad pengenalan", "KAD PENGENAWAN", "KAD PENGENALAN MyKad", "KAD" };


            var line = lines.Where(a =>
                    strValues.Any(s => s.ToLower().Trim() == a.Text.ToLower().Trim()) &&
                    a.Geometry.BoundingBox.Left < 0.5)
                .OrderBy(a => a.Geometry.BoundingBox.Top)
                .ThenBy(a => a.Geometry.BoundingBox.Left)
                .FirstOrDefault();

            AWSXRayRecorder.Instance.EndSubsegment();

            return line;
        }

        private class PostcodeCsvRow
        {
            public string PostCode { get; set; }
            public string City { get; set; }
            public string State { get; set; }
        }

        private class MalaysianState
        {
            public string name { get; set; }
        }
    }
}