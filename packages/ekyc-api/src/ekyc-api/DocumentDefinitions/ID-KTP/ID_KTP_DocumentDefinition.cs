using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using Amazon.Textract.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions
{
    public class ID_KTP_DocumentDefinition : DocumentDefinitionBase
    {
        /// <summary>
        ///     The maximum threshold that a header left corner will be.
        /// </summary>
        private const double HeaderLeftThreshold = 0.25f;

        /// <summary>
        ///     The threshold for differences in the left coordinate of boxes that constitute the same field.
        /// </summary>
        private const double FieldLeftDiffThreshold = 0.05f;

        private readonly ILogger<ID_KTP_DocumentDefinition> _logger;

        private readonly IAmazonTextract _textractClient;

        /// <summary>
        ///     The fields that will have data extracted.
        /// </summary>
        private readonly string[] FieldsOfInterest =
        {
            "IssuePlace", "IssueDate", "IssueProvince", "IssueArea", "NIK", "N.I.K", "Nama", "Gol. Darah", "Alamat",
            "Agama", "Kel/Desa", "RT/RW", "Kelurahan", "Kecamatan", "Jenis Kelamin", "TempatLahir", "TanggalLahir",
            "Pekerjaan", "Status Perkawinan", "Kewarganegaraan", "Berlaku Hingga"
        };

        public ID_KTP_DocumentDefinition(IConfiguration config, IAmazonRekognition rekognition, IAmazonS3 s3,
            IAmazonTextract amazonTextract, ILogger<ID_KTP_DocumentDefinition> logger, IAmazonTextract textract) : base(
            config, rekognition, s3, textract)
        {
            _textractClient = amazonTextract;

            _logger = logger;
        }


        public override bool LivenessSupported
        {
            get => true;
            set { }
        }

        public override string Name
        {
            get => "Indonesian KTP";

            set { }
        }

        public override DocumentTypes DocumentType
        {
            get { return DocumentTypes.ID_KTP; }
            set { }
        }

        public override NamedBoundingBox[] Landmarks { get; set; }
        public override NamedBoundingBox[] DataFields { get; set; }
        public override string RekognitionCustomLabelsProjectArn { get; set; }

        public override bool SignatureExtractionSupported
        {
            get => false;
            set { }
        }


        public override bool FaceExtractionSupported
        {
            get => true;
            set { }
        }

        private async Task<bool> CheckDocumentPerspective(List<Block> lines)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("ID_KTP_DocumentDefinition::CheckDocumentPerspective");

            var nikLine = lines.FirstOrDefault(a => a.Text.ToLower() == "nik" || a.Text.ToLower() == "n.i.k");

            if (nikLine == null)
                throw new Exception("Could not find the NIK on this KTP.");

            // Get the last line

            var lastLine = lines.OrderByDescending(a => a.Geometry.BoundingBox.Top).FirstOrDefault();

            // Check if they are inline

            var inline = Math.Abs(lastLine.Geometry.BoundingBox.Left - nikLine.Geometry.BoundingBox.Left) < 0.02;

            return inline;
        }

        public override async Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("ID_KTP_DocumentDefinition::GetFieldData");

            var request = new DetectDocumentTextRequest
            {
                Document = new Document
                {
                    S3Object = new S3Object
                    {
                        Bucket = Globals.StorageBucket,
                        Name = S3Key
                    }
                }
            };

            var response = await _textractClient.DetectDocumentTextAsync(request);

            var lines = response.Blocks.Where(x => x.BlockType == BlockType.LINE)
                .OrderBy(x => x.Geometry.BoundingBox.Top)
                .ThenBy(x => x.Geometry.BoundingBox.Left)
                .ToList();

            if (!CheckDocumentPerspective(lines).GetAwaiter().GetResult())
                throw new Exception(
                    "The document's perspective is incorrect. Please take a photo of the document directly above it so that the edges are squared.");

            dynamic returnValues = new ExpandoObject();

            if (lines.Count > 4)
            {
                // first 2 lines are going to be issue province and area
                returnValues.IssueProvince = lines[0].Text;

                returnValues.IssueArea = lines[1].Text;
            }
            // Try to get issue place and issue date

            var issueLines = lines.Where(a => a.Geometry.BoundingBox.Left > 0.75)
                .OrderBy(a => a.Geometry.BoundingBox.Top)
                .ThenBy(a => a.Geometry.BoundingBox.Left)
                .ToList();

            if (issueLines.Count >= 2)
            {
                returnValues.IssuePlace = issueLines[0].Text;
                returnValues.IssueDate = issueLines[1].Text;
            }

            const string PoBDoB = "Tempat/Tgl Lahir";
            const string Gender = "Jenis kelamin";
            const string Name = "Nama";
            const string Address = "Alamat";
            const string RTRW = "RT/RW";

            foreach (var line in lines)
            {
                // Find the value block
                var currentText = line.Text.Trim();

                if (currentText.Contains(":")) // We need to split the word
                {
                    var lineStrings = currentText.Split(":", StringSplitOptions.RemoveEmptyEntries);
                    if (lineStrings.Length >= 2)
                    {
                        var headerText = lineStrings[0].Trim();
                        var valueText = string.Join(":", lineStrings.Skip(1)).Trim();
                        if (FieldsOfInterest.Any(hn => headerText.ToLower().Trim().Equals(hn.ToLower().Trim())))
                            ((IDictionary<string, object>)returnValues)[headerText] = valueText;

                        string[] HeaderList = { Name, PoBDoB, Gender, Address, RTRW };

                        for (var iHeader = 0; iHeader < HeaderList.Length - 1; iHeader++)
                            if (HeaderList[iHeader].ToLower().Trim().StartsWith(headerText.ToLower().Trim()))
                            {
                                var nextValues = GetNextValueLines(lines, line, HeaderList[iHeader + 1]);

                                var strNextValues = string.Join(" ", nextValues.Select(a => a.Text));

                                if (!string.IsNullOrEmpty(strNextValues))
                                    valueText += " " + strNextValues;
                                ((IDictionary<string, object>)returnValues)[headerText] = valueText;
                            }
                    }

                    if (currentText.StartsWith(PoBDoB) && currentText.Length > PoBDoB.Length)
                    {
                        // get the place of birth/dob
                        currentText = currentText.Substring(PoBDoB.Length + 1).Trim();

                        if (currentText.StartsWith(":"))
                            currentText = currentText.Substring(1);

                        var values = currentText.Split(",");

                        if (values.Length >= 2)
                        {
                            var PoB = string.Join(",", values.Take(values.Length - 1)).Trim();
                            var DoB = currentText.Split(",")[values.Length - 1].Trim();

                            returnValues.TempatLahir = PoB;
                            returnValues.TanggalLahir = DoB;
                        }
                        else if (values.Length == 1)
                        {
                            // Test if the value is a date
                            DateTime dtDoB;
                            if (DateTime.TryParse(values[0], out dtDoB))
                                returnValues.TanggalLahir = values[0]; // It's a date so fill the DOB
                            else
                                returnValues.TempatLahir = values[0]; // not a date, so must be a place
                        }
                    }
                    else
                    {
                        if (FieldsOfInterest.Any(hn => currentText.ToLower().Trim().Equals(hn.ToLower().Trim())))
                        {
                            // Find the first text item on the same line to the right2
                            var valueLineBlock = lines.OrderBy(x => x.Geometry.BoundingBox.Left).FirstOrDefault(
                                x =>
                                    x.Id != line.Id &&
                                    x.Geometry.BoundingBox.Left > line.Geometry.BoundingBox.Left &&
                                    !string.IsNullOrEmpty(x.Text) && x.Text.Trim() != ":" &&
                                    Math.Abs(x.Geometry.BoundingBox.Top - line.Geometry.BoundingBox.Top) <=
                                    0.02);

                            if (valueLineBlock != null)
                            {
                                var valueText = valueLineBlock.Text.Trim();

                                if (valueText.StartsWith(":"))
                                    valueText = valueText.Substring(1);

                                if (valueText.EndsWith(":"))
                                    valueText = valueText.Substring(0, valueText.Length - 1);

                                if (currentText == "Kewarganegaraan" && valueText.Trim() == "WNF")
                                    valueText =
                                        "WNI"; // To cope with conditions where poor quality was causing misreads

                                ((IDictionary<string, object>)returnValues)[currentText] = valueText.Trim();
                            }
                        }
                    }
                }
            }


            if (returnValues.Count == 0)
            {
                return await base.GetFieldData(S3Key, docType);
            }

            Console.WriteLine($"KTP OCR Fields: {returnValues}");

            return returnValues;
        }


        private List<Block> GetNextValueLines(List<Block> lines, Block currentValueLine, string ExpectedHeaderName)
        {
            var currentLineBottom = currentValueLine.Geometry.BoundingBox.Top +
                                    currentValueLine.Geometry.BoundingBox.Height;

            var nextLine = lines.Where(x =>
                    x.Geometry.BoundingBox.Top > currentLineBottom &&
                    x.Geometry.BoundingBox.Left < HeaderLeftThreshold &&
                    x.Text.ToLower().Trim().StartsWith(ExpectedHeaderName.ToLower().Trim()))
                .OrderBy(x => x.Geometry.BoundingBox.Top)
                .ThenBy(x => x.Geometry.BoundingBox.Left)
                .FirstOrDefault();

            if (nextLine == null) // Can't find the next header name
                return new List<Block>();
            // Let's look for a value line 

            var values = lines.Where(x => x.Geometry.BoundingBox.Top > currentLineBottom
                                          && x.Geometry.BoundingBox.Top + x.Geometry.BoundingBox.Height <
                                          nextLine.Geometry.BoundingBox.Top
                                          && Math.Abs(x.Geometry.BoundingBox.Left -
                                                      currentValueLine.Geometry.BoundingBox.Left) <=
                                          FieldLeftDiffThreshold
                                          && !string.IsNullOrEmpty(x.Text))
                .OrderBy(x => x.Geometry.BoundingBox.Top)
                .ThenBy(x => x.Geometry.BoundingBox.Left).ToList();

            return values;
        }
    }
}