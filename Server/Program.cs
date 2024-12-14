using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Kennedy.Server.Controllers;
using Microsoft.Extensions.Configuration;
using RocketForce;

namespace Kennedy.Server;

class Program
{
    static void Main(string[] args)
    {
        LoadSettings(args);
        Console.WriteLine($"settings '{Settings.Global.DataRoot}'");

        X509Certificate2? serverCertificate;
        if (!CertificateUtils.TryLoadCertificate(Settings.Global.CertificateFile, Settings.Global.KeyFile,
                out serverCertificate) || serverCertificate == null)
        {
            Console.WriteLine("Could not load certificate");
            return;
        }

        GeminiServer server = new GeminiServer(
            Settings.Global.Host,
            Settings.Global.Port,
            serverCertificate,
            Settings.Global.PublicRoot)
        {
            IsMaskingRemoteIPs = false
        };

        //text search
        server.OnRequest(RoutePaths.SearchRoute, SearchController.Search);
        server.OnRequest(RoutePaths.SearchStatsRoute, SearchController.Stats);
        server.OnRequest("/lucky", SearchController.LuckySearch);

        //site-search routes
        server.OnRequest(RoutePaths.SiteSearchCreateRoute, SearchController.SiteSearchCreate);
        server.OnRequest(RoutePaths.SiteSearchRunRoute, SearchController.SiteSearchRun);

        //image search
        server.OnRequest(RoutePaths.ImageSearchRoute, ImageSearchController.Search);

        //archive routes
        server.OnRequest(RoutePaths.ViewUrlFullHistoryRoute, ArchiveController.UrlFullHistory);
        server.OnRequest(RoutePaths.ViewUrlUniqueHistoryRoute, ArchiveController.UrlHistory);
        server.OnRequest(RoutePaths.ViewCachedRoute, ArchiveController.Cached);
        server.OnRequest(RoutePaths.ViewDiffHistoryRoute, ArchiveController.DiffHistory);
        server.OnRequest(RoutePaths.ViewDiffRoute, ArchiveController.Diff);
        server.OnRequest(RoutePaths.SearchArchiveRoute, ArchiveController.Search);
        server.OnRequest(RoutePaths.ArchiveStatsRoute, ArchiveController.Stats);

        //tool routes
        server.OnRequest(RoutePaths.SiteHealthRoute, ReportsController.SiteHealth);
        server.OnRequest(RoutePaths.DomainBacklinksRoute, ReportsController.DomainBacklinks);
        server.OnRequest(RoutePaths.CertCheckRoute, CertsController.Check);
        server.OnRequest(RoutePaths.UrlInfoRoute, SearchController.UrlInfo);

        server.OnRequest("/observatory/known-hosts", SearchController.KnownHosts);
        server.OnRequest("/observatory/security.txt", SearchController.SecurityTxt);

        //deprecate old hashtags/mentions
        server.AddRedirect(new Redirect
        {
            IsTemporary = false,
            UrlPrefix = "/delorean",
            TargetUrl = "/archive/"
        });

        //handle old "view cached" links
        server.AddRedirect(new Redirect
        {
            IsTemporary = false,
            UrlPrefix = "/cached",
            TargetUrl = "/archive/"
        });

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
