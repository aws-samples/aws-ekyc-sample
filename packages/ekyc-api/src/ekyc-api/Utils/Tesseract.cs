using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.XRay.Recorder.Core;
using RestSharp;

namespace ekyc_api.Utils
{
    /// <summary>
    /// Provides utility methods for interacting with the Tesseract OCR service.
    /// </summary>
    public class Tesseract
    {
        /// <summary>
        /// Calls the Tesseract OCR endpoint, passing in a memory stream containing an image.
        /// </summary>
        /// <param name="ms">The memory stream containing the image.</param>
        /// <param name="fileName">The name of the file.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the Tesseract response.</returns>
        public static async Task<TesseractResponse> CallTesseractApi(MemoryStream ms, string fileName)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("Tesseract::CallTesseractApi");

            var options = new RestClientOptions(Globals.OcrServiceEndpoint)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            var client = new RestClient(options);
            var request = new RestRequest("/thai", Method.Post);
            request.AlwaysMultipartFormData = true;
            request.AddFile("file", ms.ToArray(), fileName, ContentType.FormUrlEncoded);
            Console.WriteLine($"Sending request to Tesseract OCR service at {Globals.OcrServiceEndpoint}/thai");
            var response = await client.ExecuteAsync(request);

            Console.WriteLine(response.Content);
            if (response?.Content == null)
                return null;
            AWSXRayRecorder.Instance.EndSubsegment();
            return JsonSerializer.Deserialize<TesseractResponse>(response?.Content);
        }

        /// <summary>
        /// Extracts field data from the front side of a Thai ID card using Tesseract OCR.
        /// </summary>
        /// <param name="ms">The memory stream containing the image of the Thai ID card.</param>
        /// <param name="fileName">The name of the file.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the extracted field data.</returns>
        public static async Task<TesseractThaiIdFrontResponse> GetThaiIdFrontDataFromTesseract(
            MemoryStream ms,
            string fileName)
        {
            AWSXRayRecorder.Instance.BeginSubsegment("Tesseract::GetThaiIdFrontFromTesseract");

            var options = new RestClientOptions(Globals.OcrServiceEndpoint)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            ms.Seek(0, SeekOrigin.Begin);

            var client = new RestClient(options);
            var request = new RestRequest("/thai/id/front", Method.Post);
            request.AlwaysMultipartFormData = true;
            request.AddFile("file", ms.ToArray(), fileName, ContentType.FormUrlEncoded);
            Console.WriteLine($"Sending request to Tesseract OCR service at {Globals.OcrServiceEndpoint}/thai/id/front");
            var response = await client.ExecuteAsync(request);
            Console.WriteLine(response.Content);
            if (response?.Content == null)
                return null;
            AWSXRayRecorder.Instance.EndSubsegment();

            return JsonSerializer.Deserialize<TesseractThaiIdFrontResponse>(response?.Content);
        }
    }

    /// <summary>
    /// Represents the response from the Tesseract OCR service.
    /// </summary>
    public class TesseractResponse
    {
        public string result { get; set; }
    }

    /// <summary>
    /// Represents the response from the Tesseract OCR service for the front side of a Thai ID card.
    /// </summary>
    public class TesseractThaiIdFrontResponse
    {
        public string BirthdayEN { get; set; }
        public string BirthdayTH { get; set; }
        public string DateOfExpiryEN { get; set; }
        public string DateOfExpiryTH { get; set; }
        public string DateOfIssueEN { get; set; }
        public string DateOfIssueTH { get; set; }
        public string FullNameTH { get; set; }
        public string Identification_Number { get; set; }
        public string LastNameEN { get; set; }
        public string LastNameTH { get; set; }
        public string NameEN { get; set; }
        public string NameTH { get; set; }
        public string PrefixEN { get; set; }
        public string PrefixTH { get; set; }
        public string Religion { get; set; }

        /// <summary>
        /// Converts the response object to a dictionary.
        /// </summary>
        /// <returns>A dictionary containing the field names and their corresponding values.</returns>
        public Dictionary<string, string> ToDictionary()
        {
            var result = new Dictionary<string, string>();
            foreach (var prop in GetType().GetProperties())
            {
                var value = prop.GetValue(this) as string;
                if (!string.IsNullOrEmpty(value)) result[prop.Name] = value;
            }

            return result;
        }
    }
}