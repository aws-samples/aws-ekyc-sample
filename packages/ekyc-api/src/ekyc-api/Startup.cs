using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace ekyc_api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;

        AWSXRayRecorder.InitializeInstance(configuration);
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    public static IConfiguration Configuration { get; private set; }


    // This method gets called by the runtime. Use this method to add services to the container
    public void ConfigureServices(IServiceCollection services)
    {
        //   services.AddSingleton<IAmazonCognitoIdentityProvider>(cognitoIdentityProvider);
        //   services.AddSingleton<CognitoUserPool>(cognitoUserPool);

        services.AddTransient<IConfiguration>(sp =>
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json");
            return configurationBuilder.Build();
        });

        AWSSDKHandler.RegisterXRayForAllServices();

        services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

        services.ConfigServices();

        services.AddLogging();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(
                policy => { policy.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin(); });
        });


        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v2", new OpenApiInfo
            {
                Version = "v2",
                Title = "eKYC API",
                Description = "An API for eKYC prototyping",
                TermsOfService = new Uri("https://aws.amazon.com/asl/"),
                Contact = new OpenApiContact
                {
                    Name = "Amazon Web Services",
                    Email = "opensource-codeofconduct@amazon.com",
                    Url = new Uri("https://aws.amazon.com")
                },
                License = new OpenApiLicense
                {
                    Name = "Use under Amazon Software License",
                    Url = new Uri("https://aws.amazon.com/asl/")
                }
            });
            // Set the comments path for the Swagger JSON and UI.    
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });

        // var strRegion = Environment.GetEnvironmentVariable("AWSRegion");
        // var strCognitoUserPoolId = Environment.GetEnvironmentVariable("CognitoPoolId");
        // var strCognitoAppClientId = Environment.GetEnvironmentVariable("CognitoAppClientId");

        /*  services.AddAuthentication(options =>
              {
                  options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                  options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                  options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
              })
              .AddCookie()
              .AddOpenIdConnect(options =>
              {
                  options.ResponseType = "code"; 
                  options.MetadataAddress = $"https://cognito-idp.{strRegion}.amazonaws.com/{strCognitoUserPoolId}/.well-known/openid-configuration"; 
                  options.ClientId = strCognitoAppClientId;
                  options.SaveTokens = true;

              });*/

        /* services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
         {
             options.SaveToken = true;
             options.TokenValidationParameters = new TokenValidationParameters
             {
                 ValidateIssuerSigningKey = true,
                 IssuerSigningKeyResolver = (s, securityToken, identifier, parameters) =>
                 {
                     // get JsonWebKeySet from AWS
                     var json = new WebClient().DownloadString(parameters.ValidIssuer + "/.well-known/jwks.json");
                     // serialize the result
                     return System.Text.Json.JsonSerializer.Deserialize<JsonWebKeySet>(json)?.Keys;
                 },
                 ValidateIssuer = true,
                 ValidIssuer = $"https://cognito-idp.{strRegion}.amazonaws.com/{strCognitoUserPoolId}",
                 ValidateLifetime = true,
                 LifetimeValidator = (before, expires, token, param) => expires > DateTime.UtcNow,
                 ValidateAudience = true,
                 ValidAudience = System.Environment.GetEnvironmentVariable("CognitoAppClientId")
             };
         });*/

        services.AddControllers();

        var serviceProvider = services.BuildServiceProvider();

        //DocumentDefinition.ServiceProvider = serviceProvider;

        S3Utils.ServiceProvider = serviceProvider;
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseExceptionHandler(c => c.Run(async context =>
        {
            var exception = context.Features
                .Get<IExceptionHandlerPathFeature>()
                .Error;

            var result = JsonSerializer.Serialize(new { error = exception.Message });
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(result);
        }));

        app.UseXRay("eKYCAPI");

        ServiceActivator.Configure(app.ApplicationServices);

        // Make sure you call this before calling app.UseMvc()
        //app.UseCors(Globals.CorsPolicyName); // allow credentials

        app.UseSwagger();


        if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

        //app.UseHttpsRedirection();


        // If not already enabled, you will need to enable ASP.NET Core authentication
        app.UseRouting();

        app.UseCors(
            options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
        );

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", async context => { await context.Response.WriteAsync("eKYC API"); });
        });


        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v2/swagger.json", "eKYC API");
            //    c.RoutePrefix = string.Empty;
        });


        Console.WriteLine("Service running");
    }
}