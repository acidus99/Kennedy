using System;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RocketForce;

namespace Kennedy.Server
{
    class Program
    {
        static void Main(string[] args)
        {

            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss ";
                })
                .SetMinimumLevel(LogLevel.Debug)
            );

            // Build a config object, using env vars and JSON providers.
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ENV")}.json")
                .Build();

            // Get values from the config given their key and their target type.
            Settings settings = config.GetSection("Settings").Get<Settings>();
            Settings.Global = settings;

            App app = new App(
                settings.Host,
                settings.Port,
                CertificateUtils.LoadCertificate(settings.CertificateFile, settings.KeyFile),
                settings.PublicRoot,
                settings.AccessLogPath
            );
            app.Logger = loggerFactory.CreateLogger<App>();
            app.IsMaskingRemoteIPs = false;

            app.OnRequest("/search", SearchController.Search);
            app.OnRequest("/lucky", SearchController.LuckySearch);
            app.OnRequest("/observatory/known-hosts", SearchController.KnownHosts);
            app.OnRequest("/observatory/security.txt", SearchController.SecurityTxt);
            app.OnRequest("/delorean", SearchController.DeloreanSearch);
            app.OnRequest("/cached", SearchController.Cached);
            app.OnRequest("/page-info", SearchController.PageInfo);
            app.Run();
        }
    }
}