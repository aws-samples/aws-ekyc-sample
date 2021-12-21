using System;
using System.IO;
using System.Net;
using System.Reflection;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using ekyc_api.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ekyc_api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            AWSXRayRecorder.InitializeInstance(configuration);
        }

        public static IConfiguration Configuration { get; private set; }

        public static string[] AllowedOrigins
        {
            get
            {
                return new string[]
                {
                    "http://localhost:3000",
                    $"https://{System.Environment.GetEnvironmentVariable("OriginDistributionDomain")}"
                };
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(name: Globals.CorsPolicyName,
                    builder =>
                    {
                        builder.WithOrigins(AllowedOrigins)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .DisallowCredentials();
                    });
            });

            services.AddCors();

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


            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "eKYC API",
                    Description = "An API for eKYC prototyping",
                    TermsOfService = new Uri("https://example.com/terms"),
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

            string strRegion = System.Environment.GetEnvironmentVariable("AWSRegion");
            string strCognitoUserPoolId = System.Environment.GetEnvironmentVariable("CognitoPoolId");
            string strCognitoAppClientId = System.Environment.GetEnvironmentVariable("CognitoAppClientId");

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
            ServiceActivator.Configure(app.ApplicationServices);

            // Make sure you call this before calling app.UseMvc()
            app.UseCors(Globals.CorsPolicyName); // allow credentials

            app.UseXRay("eKYCAPI");

            app.UseSwagger();


            app.Use((context, next) =>
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = string.Join(",", AllowedOrigins);
                context.Response.Headers["Access-Control-Allow-Headers"] =
                    "Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token,X-Amz-User-Agent";
                context.Response.Headers["Access-Control-Allow-Methods"] = "OPTIONS,GET,PUT,POST,DELETE,PATCH,HEAD";
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                return next.Invoke();
            });


            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseExceptionHandler(c => c.Run(async context =>
            {
                var exception = context.Features
                    .Get<IExceptionHandlerPathFeature>()
                    .Error;

                var result = JsonSerializer.Serialize(new { error = exception.Message });
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(result);
            }));

            app.UseHttpsRedirection();


            // If not already enabled, you will need to enable ASP.NET Core authentication
            app.UseAuthentication();
            app.UseRouting();

            app.UseAuthorization();


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGet("/", async context => { await context.Response.WriteAsync("eKYC API"); });
            });


            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "eKYC API");
                //    c.RoutePrefix = string.Empty;
            });


            Console.WriteLine("Service running");
        }
    }
}