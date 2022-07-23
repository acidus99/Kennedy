using System;
using System.IO;
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
            LoadSettings(args);

            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss ";
                })
                .SetMinimumLevel(LogLevel.Debug)
            );

            Console.WriteLine($"settings '{Settings.Global.DataRoot}'");

            App app = new App(
                Settings.Global.Host,
                Settings.Global.Port,
                CertificateUtils.LoadCertificate(Settings.Global.CertificateFile, Settings.Global.KeyFile),
                Settings.Global.PublicRoot,
                Settings.Global.AccessLogPath
            );
            app.Logger = loggerFactory.CreateLogger<App>();
            app.IsMaskingRemoteIPs = false;

            //text search
            app.OnRequest("/search", SearchController.Search);
            app.OnRequest("/lucky", SearchController.LuckySearch);

            //image search
            app.OnRequest("/image-search", ImageSearchController.Search);

            app.OnRequest("/observatory/known-hosts", SearchController.KnownHosts);
            app.OnRequest("/observatory/security.txt", SearchController.SecurityTxt);
            app.OnRequest("/delorean", SearchController.DeloreanSearch);
            app.OnRequest("/cached", SearchController.Cached);
            app.OnRequest("/page-info", SearchController.PageInfo);
            app.Run();
        }

        static void LoadSettings(string[] args)
        {
            string settingsFile = "";
            if (args.Length > 0)
            {
                Console.Write($"Using setting file via command line '{args[0]}'");
                settingsFile = args[0];
            }
            else if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENV")))
            {
                Console.Write($"Using settings file via environment variables ");
                settingsFile = $"appsettings.{Environment.GetEnvironmentVariable("ENV")}.json";
            }
            Console.WriteLine($"'{settingsFile}");
            if(!File.Exists(settingsFile))
            {
                Console.WriteLine("Could not locate settings file");
                System.Environment.Exit(1);
            }
            // Build a config object, using env vars and JSON providers.
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile(settingsFile)
                .Build();
            // Get values from the config given their key and their target type.
            Settings.Global = config.GetSection("Settings").Get<Settings>();
        }
    }
}