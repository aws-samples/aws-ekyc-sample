using System;
using System.IO;
using System.Threading.Tasks;
using RestSharp;

namespace ekyc_api.Utils;

public class Tesseract
{
    public class TesseractResponse
    {
        public string result { get; set; }
    }
    public static async Task<TesseractResponse> CallTesseractApi(MemoryStream ms)
    {
        var options = new RestClientOptions("http://localhost:8000")
        {
            MaxTimeout = -1,
        };
        var client = new RestClient(options);
        var request = new RestRequest("/thai", Method.Post);
        request.AlwaysMultipartFormData = true;
        request.AddFile("file",ms.ToArray(),"image.png", ContentType.FormUrlEncoded);
        RestResponse response = await client.ExecuteAsync(request);
        Console.WriteLine(response.Content);
        if (response?.Content == null)
            return null;
        return System.Text.Json.JsonSerializer.Deserialize<TesseractResponse>(response?.Content);
    }

}