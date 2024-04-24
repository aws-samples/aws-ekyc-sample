using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ekyc_api;

/// <summary>
///     The Main function can be used to run the ASP.NET Core application locally using the Kestrel webserver.
/// </summary>
public class LocalEntryPoint
{
    public static void Main(string[] args)
    {
        //SetEnvVars();

        var builder = CreateHostBuilder(args).Build();

        builder.Run();
    }


    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>()
                    .ConfigureKestrel(options => options.ListenAnyIP(6500));
            })
            .ConfigureLogging(
                logging =>
                {
                    logging.AddAWSProvider();

                    // When you need logging below set the minimum level. Otherwise the logging framework will default to Informational for external providers.
                    logging.SetMinimumLevel(LogLevel.Debug);
                });
    }
}