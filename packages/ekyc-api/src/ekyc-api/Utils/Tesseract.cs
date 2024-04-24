using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.XRay.Recorder.Core;
using RestSharp;

namespace ekyc_api.Utils;

public class Tesseract
{
    public static async Task<TesseractResponse> CallTesseractApi(MemoryStream ms, string fileName)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("Tesseract::CallTesseractApi");


        var options = new RestClientOptions(Globals.OcrServiceEndpoint)
        {
            MaxTimeout = 15000
        };
        var client = new RestClient(options);
        var request = new RestRequest("/thai", Method.Post);
        request.AlwaysMultipartFormData = true;
        request.AddFile("file", ms.ToArray(), fileName, ContentType.FormUrlEncoded);
        var response = await client.ExecuteAsync(request);

        Console.WriteLine(response.Content);
        if (response?.Content == null)
            return null;
        AWSXRayRecorder.Instance.EndSubsegment();
        return JsonSerializer.Deserialize<TesseractResponse>(response?.Content);
    }


    public static async Task<TesseractThaiIdFrontResponse> GetThaiIdFrontDataFromTesseract(
        MemoryStream ms,
        string fileName)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("Tesseract::GetThaiIdFrontFromTesseract");

        var options = new RestClientOptions(Globals.OcrServiceEndpoint)
        {
            MaxTimeout = 15000
        };

        ms.Seek(0, SeekOrigin.Begin);

        var client = new RestClient(options);
        var url = "/thai/id/front";
        var request = new RestRequest(url, Method.Post);
        request.AlwaysMultipartFormData = true;
        request.AddFile("file", ms.ToArray(), fileName, ContentType.FormUrlEncoded);
        var response = await client.ExecuteAsync(request);
        Console.WriteLine(response.Content);
        if (response?.Content == null)
            return null;
        AWSXRayRecorder.Instance.EndSubsegment();

        return JsonSerializer.Deserialize<TesseractThaiIdFrontResponse>(response?.Content);
    }
}

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

public class TesseractResponse
{
    public string result { get; set; }
}