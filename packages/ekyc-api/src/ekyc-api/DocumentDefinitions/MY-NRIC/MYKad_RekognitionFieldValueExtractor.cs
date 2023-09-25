using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.XRay.Recorder.Core;
using CsvHelper;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions
{
    public class MYKad_RekognitionFieldValueExtractor : IFieldValueExtractor
    {
        private const double HeaderLeftThreshold = 0.25d; // The maximum threshold that a header left corner will be

        private const double
            FieldLeftDiffThreshold =
                0.05d; // The threshold for differences in the left coordinate of boxes that constitute the same field

        private readonly IConfiguration _config;

        private readonly ILogger _logger;

        private readonly IAmazonRekognition _rekognitionClient;

        private List<TextDetection> TextDetections;

        public MYKad_RekognitionFieldValueExtractor(IAmazonRekognition rekognition, IConfiguration config,
            ILogger logger)
        {
            _rekognitionClient = rekognition;
            _config = config;
            _logger = logger;
        }

        public async Task<Dictionary<string, string>> GetFieldValues(string s3Key,string RekognitionCustomLabelsProjectArn,
            DocumentTypes documentType)
        {
            if (!File.Exists("states.json"))
                throw new Exception("Malaysian states file not defined.");

            List<MalaysianState> states = null;

            using (var sr = File.OpenText("states.json"))
            {
                states = JsonSerializer.Deserialize<List<MalaysianState>>(sr.ReadToEnd());
            }

            AWSXRayRecorder.Instance.BeginSubsegment("RekognitionTextValueExtractor::GetFieldValues");

            if (TextDetections == null) await LoadTextDetections(s3Key);


            var lines = TextDetections.Where(x => x.Type == TextTypes.LINE)
                .OrderBy(x => x.Geometry.BoundingBox.Top)
                .ThenBy(x => x.Geometry.BoundingBox.Left)
                .ToList();


            foreach (var line in lines)
                _logger.LogDebug(
                    $"{line.DetectedText} - X: {line.Geometry.BoundingBox.Left} Y: {line.Geometry.BoundingBox.Top} Width: {line.Geometry.BoundingBox.Width} Height: {line.Geometry.BoundingBox.Height}");

            dynamic values = new ExpandoObject();

            var topLeftKadBlock = GetTopLeftKadBlock(lines);

            if (topLeftKadBlock == null)
            {
                AWSXRayRecorder.Instance.EndSubsegment();
                throw new Exception("Unable to find myKad landmark - Kad Pengenalan");
            }

            /* if (!CheckDocumentPerspective(s3Key, topLeftKadBlock).GetAwaiter().GetResult())
             {
                 AWSXRayRecorder.Instance.EndSubsegment();
                 throw new Exception("The document's perspective is incorrect. Please take a photo of the document directly above it so that the edges are squared.");
             }*/

            var regexNRIC = new Regex(@"^\d{6}-\d{2}-?(?:\d{4})?$", RegexOptions.Compiled);

            var nricLine = lines.FirstOrDefault(x =>
                x.Geometry.BoundingBox.Top - topLeftKadBlock.Geometry.BoundingBox.Top <= 0.3
                && x.Geometry.BoundingBox.Left <= topLeftKadBlock.Geometry.BoundingBox.Left
                && x.Geometry.BoundingBox.Left + x.Geometry.BoundingBox.Width <
                topLeftKadBlock.Geometry.BoundingBox.Left +
                topLeftKadBlock.Geometry.BoundingBox.Width
                && regexNRIC.IsMatch(x.DetectedText));

            if (nricLine != null)
            {
                if (!string.IsNullOrEmpty(nricLine.DetectedText))
                    values["NRIC"] = nricLine.DetectedText;
            }
            else
            {
                var ex = new Exception("Unable to find the NRIC number on this IC.");
                AWSXRayRecorder.Instance.AddException(ex);

                AWSXRayRecorder.Instance.EndSubsegment();
                throw ex;
            }

            // Get the state line

            var stateLine = lines.FirstOrDefault(x =>
                states.Any(s => s.name.ToLower().Trim() == x.DetectedText.Replace(" ", "").ToLower().Trim()));

            if (stateLine == null)
            {
                var ex = new Exception("Unable to find the user's state on this IC.");
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
                    strName += line.DetectedText;
                else
                    strName += " " + line.DetectedText;

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
                    !string.IsNullOrEmpty(a.DetectedText)
                    && a.Geometry.BoundingBox.Top > currentMaxTop
                    && a.Geometry.BoundingBox.Top <= stateLine.Geometry.BoundingBox.Top
                )
                .OrderBy(a => a.Geometry.BoundingBox.Top)
                .ThenBy(a => a.Geometry.BoundingBox.Left)
                .ToList();

            dynamic Address = new ExpandoObject();
            var regexPostcode = new Regex(@"^(?<postcode>\d{4,5})\s(?<area>.+)$", RegexOptions.Compiled);
            for (var i = 0; i < addressLines.Count; i++)
                if (regexPostcode.IsMatch(addressLines[i].DetectedText))
                {
                    var matches = regexPostcode.Matches(addressLines[i].DetectedText);
                    if (matches.Count > 0)
                    {
                        Address.Postcode = matches[0].Groups["postcode"].Value;
                        // Look up the area via CSV
                        if (postcodeData.Any(a => a.PostCode == Address.Postcode))
                        {
                            var postcodeRow = postcodeData.First(a => a.PostCode == Address.Postcode);
                            Address.City = postcodeRow.City;
                            Address.State = postcodeRow.State;
                        }
                        else
                        {
                            Address.City = matches[0].Groups["area"].Value;
                        }
                    }
                    else
                    {
                        ((IDictionary<string, object>)Address)["Line_" + (i + 1)] = addressLines[i].DetectedText;
                    }
                }
                else
                {
                    ((IDictionary<string, object>)Address)["Line_" + (i + 1)] = addressLines[i].DetectedText;
                }

            values["Address"] = JsonSerializer.Serialize(Address);

            // Try to get the gender/religion line
            var male = "lelaki";
            var female = "perempuan";
            var genderLine = lines.FirstOrDefault(a =>
                a.DetectedText.ToLower().EndsWith(female) || a.DetectedText.ToLower().EndsWith(male));
            if (genderLine != null)
            {
                var splitGenderLine = genderLine.DetectedText.Trim().Split(" ");

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
                                                                 && !string.IsNullOrEmpty(a.DetectedText)
                    );
                    if (religionLine != null)
                        values["Religion"] = religionLine.DetectedText.Trim();
                }
            }


            var strDict = JsonSerializer.Serialize(values);
            Console.WriteLine($"MyKAD OCR Fields: {strDict}");

            AWSXRayRecorder.Instance.EndSubsegment();

            return values;
        }

        private async Task LoadTextDetections(string s3Key)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("RekognitionTextValueExtractor::LoadTextDetections");
            var response = await _rekognitionClient.DetectTextAsync(new DetectTextRequest
            {
                Image = new Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = s3Key
                    }
                }
            });
            TextDetections = response.TextDetections;

            AWSXRayRecorder.Instance.EndSubsegment();
        }
/*
        private async Task<bool> CheckDocumentPerspective(string s3Key,TextDetection topLeftKadBlock)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("RekognitionTextValueExtractor::CheckDocumentPerspective");
            
            if (TextDetections == null)
            {
                await LoadTextDetections(s3Key);
            }
            
            var sortedBlocks = TextDetections
                .OrderBy(x => x.Geometry.BoundingBox.Top)
                .ThenBy(x => x.Geometry.BoundingBox.Left)
                .ToList();

            var lines = sortedBlocks
                .Where(b => b.Type == TextTypes.LINE)
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
        private TextDetection GetTopLeftKadBlock(List<TextDetection> lines)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("RekognitionTextValueExtractor::GetTopLeftKadBlock");

            string[] strValues = { "kad pengenalan", "KAD PENGENAWAN", "KAD PENGENALAN MyKad" };


            var line = lines.Where(a => strValues.Any(s => s.ToLower().Trim() == a.DetectedText.ToLower().Trim()))
                .OrderBy(a => a.Geometry.BoundingBox.Top)
                .ThenBy(a => a.Geometry.BoundingBox.Left)
                .FirstOrDefault();

            AWSXRayRecorder.Instance.EndSubsegment();

            return line;
        }
    }
}