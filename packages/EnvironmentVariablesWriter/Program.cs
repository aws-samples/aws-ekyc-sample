// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.Model;

internal class Program
{
    private static async Task Main(string[] args)
    {
        if (args?.Length == 0) Console.WriteLine("Provide the Lambda function ARN as an argument.");

        var lambdaClient = new AmazonLambdaClient();

        var function = await lambdaClient.GetFunctionAsync(new GetFunctionRequest
        {
            FunctionName = args[0]
        });

        var sbEnvVars = new StringBuilder();

        foreach (var k in
                 function.Configuration.Environment.Variables.Keys)
            sbEnvVars.AppendFormat($"{k}={function.Configuration.Environment.Variables[k]};");

        sbEnvVars.Append(@"AWS_PROFILE=""maxbit""");

        using (var fs = File.Open("./environment.txt", FileMode.Create))
        {
            var bytes = Encoding.UTF8.GetBytes(sbEnvVars.ToString());
            fs.Write(bytes);
        }

        var json = JsonSerializer.Serialize(function.Configuration.Environment.Variables);

        using (var fs = File.Open("./local-config.json", FileMode.Create))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            fs.Write(bytes);
        }

        Console.WriteLine("Done.");
    }
}