using System;
using System.IO;
using System.Net;
using Microsoft.Extensions.Configuration;

using Kennedy.Server.Controllers;

using RocketForce;

namespace Kennedy.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            LoadSettings(args);
            Console.WriteLine($"settings '{Settings.Global.DataRoot}'");

            GeminiServer server = new GeminiServer(
                Settings.Global.Host,
                Settings.Global.Port,
                CertificateUtils.LoadCertificate(Settings.Global.CertificateFile, Settings.Global.KeyFile),
                Settings.Global.PublicRoot)
            {
                IsMaskingRemoteIPs = false
            };

            //text search
            server.OnRequest("/search", SearchController.Search);
            server.OnRequest("/lucky", SearchController.LuckySearch);

            //image search
            server.OnRequest("/image-search", ImageSearchController.Search);

            server.OnRequest(RoutePaths.ViewUrlHistoryRoute, ArchiveController.UrlHistory);
            server.OnRequest(RoutePaths.ViewCachedRoute, ArchiveController.Cached);
            server.OnRequest(RoutePaths.SearchArchiveRoute, ArchiveController.Search);
            server.OnRequest(RoutePaths.ArchiveStatsRoute, ArchiveController.Stats);
            server.OnRequest("/delorean", ArchiveController.Redirect);

            server.OnRequest(RoutePaths.SiteHealthRoute, ReportsController.SiteHealth);
            server.OnRequest(RoutePaths.DomainBacklinksRoute, ReportsController.DomainBacklinks);

            server.OnRequest("/observatory/known-hosts", SearchController.KnownHosts);
            server.OnRequest("/observatory/security.txt", SearchController.SecurityTxt);
            server.OnRequest("/page-info", SearchController.UrlInfo);

            //deprecate old hashtags/mentions
            server.AddRedirect(new Redirect
            {
                IsTemporary = false,
                UrlPrefix = "/mentions/",
                TargetUrl = "/mentions-and-hashtags.gmi"
            });
            server.AddRedirect(new Redirect
            {
                IsTemporary = false,
                UrlPrefix = "/hashtags/",
                TargetUrl = "/mentions-and-hashtags.gmi"
            });

            server.Run();
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

            var settings = config.GetSection("Settings").Get<Settings>();

            if(settings == null)
            {
                throw new ApplicationException("Could not create global settings object. Received null");
            }
            Settings.Global = settings;
        }
    }
}