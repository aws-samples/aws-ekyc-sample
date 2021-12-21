using System;
using Amazon.XRay.Recorder.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ekyc_api
{
    /// <summary>
    ///     The Main function can be used to run the ASP.NET Core application locally using the Kestrel webserver.
    /// </summary>
    public class LocalEntryPoint
    {
        public static void Main(string[] args)
        {
            SetEnvVars();

            var builder = CreateHostBuilder(args).Build();

            builder.Run();
        }

        public static void SetEnvVars()
        {
            Environment.SetEnvironmentVariable("SessionTable",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("DataRequestsTable",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("VerificationHistoryTable",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("StorageBucket",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("RekognitionDocumentTypesArn",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("ApprovalsSnsTopic",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("RekognitionArnParameterName",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("GroundTruthUiTemplateS3Uri",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("TrainingBucket",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("TrainingTableName",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("GroundTruthRoleArn",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("GroundTruthUiTemplateS3Uri",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("GroundTruthWorkteamArn",
                "<REPLACE>");

            Environment.SetEnvironmentVariable("Environment", "Unit Test");
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
                .ConfigureLogging(
                    logging =>
                    {
                        logging.AddAWSProvider();

                        // When you need logging below set the minimum level. Otherwise the logging framework will default to Informational for external providers.
                        logging.SetMinimumLevel(LogLevel.Debug);
                    });
        }
    }
}