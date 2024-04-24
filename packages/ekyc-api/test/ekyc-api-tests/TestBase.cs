using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.XRay.Recorder.Core;
using ekyc_api;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace ekyc_api_tests;

public abstract class TestBase
{
    public TestBase()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false)
            .Build();

        Configuration = config;

        AWSXRayRecorder.Instance.BeginSegment("Unit Test");

        TestHost = CreateHostBuilder().Build();


        Task.Run(() => TestHost.RunAsync());
    }

    public IConfiguration Configuration { get; }

    public IHost TestHost { get; }

    [TearDown]
    public void Cleanup()
    {
        AWSXRayRecorder.Instance.EndSegment();
    }

    public async Task PutObjectAtUrl(string Url, string LocalPath)
    {
        var httpRequest = WebRequest.Create(Url) as HttpWebRequest;
        httpRequest.Method = "PUT";
        using (var dataStream = httpRequest.GetRequestStream())
        {
            var buffer = new byte[8000];
            await using (var fileStream = new FileStream(LocalPath, FileMode.Open,
                             FileAccess.Read))
            {
                var bytesRead = 0;
                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    await dataStream.WriteAsync(buffer, 0, bytesRead);
            }
        }

        var response = httpRequest.GetResponse() as HttpWebResponse;
        Console.WriteLine("Upload response: " + response.StatusCode);
    }

    public IHostBuilder CreateHostBuilder(string[] args = null)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", true);
                config.AddEnvironmentVariables();

                if (args != null) config.AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddOptions();

                services.AddHttpClient();

                services.AddTransient<IConfiguration>(sp =>
                {
                    IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
                    configurationBuilder.AddJsonFile("appsettings.json");
                    return configurationBuilder.Build();
                });

                services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

                services.ConfigServices();

                var serviceProvider = services.BuildServiceProvider();

                //DocumentDefinition.ServiceProvider = serviceProvider;

                S3Utils.ServiceProvider = serviceProvider;
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                logging.AddConsole();
            });

        return host;
    }
}