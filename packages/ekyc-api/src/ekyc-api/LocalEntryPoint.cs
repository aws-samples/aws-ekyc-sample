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
                "EkycInfraStack-storageSessions515A5702-XWXPR91G8VY3");

            Environment.SetEnvironmentVariable("DataRequestsTable",
                "EkycInfraStack-storageDataRequestsF5098A37-SQFHLBCCVO6A");

            Environment.SetEnvironmentVariable("VerificationHistoryTable",
                "EkycInfraStack-storageVerificationHistoryB8330F3D-16F7DUUZV5IU");

            Environment.SetEnvironmentVariable("StorageBucket",
                "ekycinfrastack-storagestoragebucketb86286fa-stjknmdcaz2z");

            Environment.SetEnvironmentVariable("RekognitionDocumentTypesArn",
                "arn:aws:rekognition:ap-southeast-1:094168707463:project/eKYC/version/eKYC.2021-10-21T20.52.22/1634820742781");

            Environment.SetEnvironmentVariable("ApprovalsSnsTopic",
                "arn:aws:sns:ap-southeast-1:331188376512:EkycInfraStack-topicsekycapprovaltopic27D1BBB1-SR58ST9X0O9X");

            Environment.SetEnvironmentVariable("RekognitionArnParameterName",
                "CFN-parametersekycrekognitionarn7746EDAA-5xElptoOgqbg");

            Environment.SetEnvironmentVariable("GroundTruthUiTemplateS3Uri",
                "s3://sagemaker-data-020220201/labellers.html");

            Environment.SetEnvironmentVariable("TrainingBucket",
                "sagemaker-data-020220201");

            Environment.SetEnvironmentVariable("TrainingTableName",
                "EkycInfraStack-storageTrainingJobs6DCD8424-PAMGF7VSGMVH");

            Environment.SetEnvironmentVariable("GroundTruthRoleArn",
                "arn:aws:iam::331188376512:role/GroundTruthArn");

            Environment.SetEnvironmentVariable("GroundTruthUiTemplateS3Uri",
                "s3://sagemaker-data-020220201/workertemplate.html");

            Environment.SetEnvironmentVariable("GroundTruthWorkteamArn",
                "arn:aws:sagemaker:ap-southeast-1:331188376512:workteam/private-crowd/labellers");

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