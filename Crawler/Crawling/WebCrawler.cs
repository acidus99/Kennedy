using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;


using Gemini.Net;
using Kennedy.Crawler.Frontiers;
using Kennedy.Crawler.Logging;
using Kennedy.Crawler.Protocols;
using Kennedy.Crawler.Utils;

using Kennedy.CrawlData;

using Kennedy.Data;
using Kennedy.Parsers;
using Kennedy.Crawler.Domains;


namespace Kennedy.Crawler.Crawling;

public class WebCrawler : IWebCrawler
{
    const int StatusIntervalDisk = 60000;
    const int StatusIntervalScreen = 5000;

    /// <summary>
    /// how long should we wait between requests
    /// </summary>
    const int delayMs = 350;

    int CrawlerThreads;
    ThreadSafeCounter TotalUrlsRequested;
    ThreadSafeCounter TotalUrlsProcessed;
    ErrorLog errorLog;

    int UrlLimit;

    bool HitUrlLimit
        => (TotalUrlsRequested.Count >= UrlLimit);

    IUrlFrontier UrlFrontier;
    UrlFrontierWrapper FrontierWrapper;

    SeenContentTracker seenContentTracker;

    ResponseParser responseParser;

    DocumentStorageSystem documentStorage;

    DomainAnalyzer domainAnalyzer;


    System.Timers.Timer DiskStatusTimer;
    Stopwatch CrawlerStopwatch;

    bool UserQuit = false;


    public WebCrawler(int crawlerThreads, int urlLimit)
    {
        CrawlerThreads = crawlerThreads;
        UrlLimit = urlLimit;

        ConfigureDirectories();

        responseParser = new ResponseParser();
        
        TotalUrlsRequested = new ThreadSafeCounter();
        TotalUrlsProcessed = new ThreadSafeCounter();

        UrlFrontier = new BalancedUrlFrontier(CrawlerThreads);
        FrontierWrapper = new UrlFrontierWrapper(UrlFrontier);
        seenContentTracker = new SeenContentTracker();

        documentStorage = new DocumentStorageSystem(CrawlerOptions.DataStore);

        domainAnalyzer = new DomainAnalyzer(documentStorage);

    }

    private void ConfigureDirectories()
    {
        Directory.CreateDirectory(CrawlerOptions.DataStore);
        Directory.CreateDirectory(CrawlerOptions.PublicRoot);
        Directory.CreateDirectory(CrawlerOptions.Logs);
        LanguageDetector.ConfigFileDirectory = CrawlerOptions.ConfigDir;
        errorLog = new ErrorLog(CrawlerOptions.ErrorLog);
    }

    private void ConfigureTimers()
    {
        CrawlerStopwatch = new Stopwatch();
        CrawlerStopwatch.Start();

        DiskStatusTimer = new System.Timers.Timer(StatusIntervalDisk)
        {
            Enabled = true,
            AutoReset = true,
        };
        DiskStatusTimer.Elapsed += LogStatusToDisk;

        DiskStatusTimer.Start();
    }

    public void AddSeed(string url)
        => UrlFrontier.AddUrl(new GeminiUrl(url));

    public void DoCrawl()
    {
        ConfigureTimers();
        for (int i = 0; i < CrawlerThreads; i++)
        {
            SpawnWorker(i);
        }
        domainAnalyzer.Start();

        int prevRequested = 0;
        do
        {
            if (Console.In is StreamReader)
            {
                if (Console.KeyAvailable)
                {
                    Console.WriteLine("stop? type 'quit'");
                    Console.ReadKey(true);
                    if (Console.ReadLine() == "quit")
                    {
                        Console.WriteLine("quiting...");
                        UserQuit = true;
                    }
                    else
                    {
                        Console.WriteLine("resuming");
                    }
                }

            }
            Thread.Sleep(StatusIntervalScreen);

            int currRequested = TotalUrlsRequested.Count;
            string speed = ComputeSpeed((double)currRequested, (double)prevRequested, (double)StatusIntervalScreen);
            Console.WriteLine($"Elapsed: {CrawlerStopwatch.Elapsed}\tActive Workers: {WorkInFlight} Speed: {speed}\tTotal Requested: {currRequested}\tTotal Processed: {TotalUrlsProcessed.Count}\tRemaining: {UrlFrontier.Count}");
            prevRequested = TotalUrlsRequested.Count;

        } while (KeepWorkersAlive);
        CrawlerStopwatch.Stop();
        domainAnalyzer.Stop();
        FinalizeCrawl();

        Console.WriteLine($"Complete! {CrawlerStopwatch.Elapsed.TotalSeconds}");
    }

    private void FinalizeCrawl()
    {
        documentStorage.Finalize();
    }

    private void SpawnWorker(int workerNum)
    {
        var worker = new WebCrawlWorker(this, workerNum);

        var threadDelegate = new ThreadStart(worker.DoWork);
        var newThread = new Thread(threadDelegate);
        newThread.Name = $"Worker {workerNum}";
        newThread.Start();
    }

    private string ComputeSpeed(double curr, double prev, double seconds)
    {
        double requestSec = (curr - prev) / seconds * 1000;
        return $"{requestSec} req / sec";
    }

    private void LogStatusToDisk(object? sender, System.Timers.ElapsedEventArgs e)
    {
        StatusLogger logger = new StatusLogger(CrawlerOptions.Logs);
        logger.LogStatus(FrontierWrapper);
        logger.LogStatus(UrlFrontier);
    }

    public void ProcessRequestResponse(GeminiResponse resp, Exception ex)
    {
        //null means it was ignored by robots
        if (resp != null)
        {
            bool isReachable = true;

            if (resp.ConnectStatus == ConnectStatus.Error)
            {
                var msg = ex?.Message ?? resp.Meta;
                errorLog.LogError(msg, resp.RequestUrl.NormalizedUrl);
                isReachable = false;
            }

            if (!seenContentTracker.CheckAndRecord(resp))
            {
                var parsedResponse = responseParser.Parse(resp);
                FrontierWrapper.AddUrls(parsedResponse.Links);
                documentStorage.StoreDocument(parsedResponse);

                domainAnalyzer.AddDomain(resp.RequestUrl.Hostname, resp.RequestUrl.Port, isReachable);
            }
        }
        TotalUrlsProcessed.Increment();
    }


    public GeminiUrl GetUrl(int crawlerID = 0)
    {
        if (HitUrlLimit || UserQuit)
        {
            return null;
        }

        var url = UrlFrontier.GetUrl(crawlerID);
        if (url != null)
        {
            TotalUrlsRequested.Increment();
        }
        return url;
    }

    /// <summary>
    /// Is there pending work in our queue?
    /// </summary>
    public bool HasUrlsToFetch
    {
        get
        {
            if (UserQuit)
            {
                return false;
            }
            return (!HitUrlLimit) ? (UrlFrontier.Count > 0) : false;
        }
    }

    /// <summary>
    /// Is there work being done
    /// </summary>
    public int WorkInFlight
        => TotalUrlsRequested.Count - TotalUrlsProcessed.Count;

    public bool KeepWorkersAlive
        => (HasUrlsToFetch || (WorkInFlight > 0));

}
