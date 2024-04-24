using System;
using Amazon.DynamoDBv2;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SageMaker;
using Amazon.SageMakerRuntime;
using Amazon.Textract;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using ekyc_api.DocumentDefinitions;
using ekyc_api.DocumentDefinitions.AU_Passport;
using ekyc_api.DocumentDefinitions.CN_Passport;
using ekyc_api.DocumentDefinitions.KH_ID;
using ekyc_api.DocumentDefinitions.SG_PASSPORT;
using ekyc_api.DocumentDefinitions.THAI_ID;
using ekyc_api.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace ekyc_api;

public static class ServicesConfig
{
    public static IServiceCollection ConfigServices(this IServiceCollection services)
    {
        var Region = Environment.GetEnvironmentVariable("AWSRegion");
        var PoolId = Environment.GetEnvironmentVariable("CognitoPoolId");
        var AppClientId = Environment.GetEnvironmentVariable("CognitoAppClientId");

        AWSSDKHandler.RegisterXRayForAllServices();

        /*services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (s, securityToken, identifier, parameters) =>
                    {
                        // Get JsonWebKeySet from AWS
                        var json = new WebClient().DownloadString(parameters.ValidIssuer + "/.well-known/jwks.json");
                        // Serialize the result
                        return JsonConvert.DeserializeObject<JsonWebKeySet>(json).Keys;
                    },
                    ValidateIssuer = true,
                    ValidIssuer = $"https://cognito-idp.{Region}.amazonaws.com/{PoolId}",
                    ValidateLifetime = true,
                    LifetimeValidator = (before, expires, token, param) => expires > DateTime.UtcNow,
                    ValidateAudience = true,
                    ValidAudience = AppClientId,
                };
            });*/

        services.AddAWSService<IAmazonS3>();

        services.AddAWSService<IAmazonRekognition>();

        services.AddAWSService<IAmazonDynamoDB>();

        services.AddAWSService<IAmazonTextract>();

        services.AddAWSService<IAmazonSageMakerRuntime>();

        services.AddAWSService<IAmazonSageMaker>();


        services.AddTransient<IDocumentDefinitionFactory, DocumentDefinitionFactory>();

        services.AddTransient<ILivenessChecker, LivenessChecker>();

        services.AddTransient<IDocumentChecker, RekognitionDocumentChecker>();

        services.AddTransient<ID_KTP_DocumentDefinition>();

        services.AddTransient<KH_IC_DocumentDefinition>();

        services.AddTransient<AU_Passport_DocumentDefinition>();

        services.AddTransient<SG_Passport_DocumentDefinition>();

        services.AddTransient<PRC_Passport_DocumentDefinition>();

        services.AddTransient<PRC_Passport_TextractFieldValueExtractor>();

        services.AddTransient<MY_NRIC_DocumentDefinition>();

        services.AddTransient<Thai_ID_Front_DocumentDefinition>();

        services.AddTransient<Thai_ID_Back_DocumentDefinition>();

        services.AddTransient<GenericDocumentDefinition>();

        services.AddTransient<IDocumentChecker, RekognitionDocumentChecker>();


        return services;
    }
}