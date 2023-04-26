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
using Kennedy.Data;
using Kennedy.Parsers;
using Kennedy.SearchIndex;

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

    ILinksFinder ResponseLinkFinder;
    ILinksFinder ProactiveLinksFinder;

    SeenContentTracker seenContentTracker;

    ResultsWriter ResultsWarc;

    System.Timers.Timer DiskStatusTimer;
    Stopwatch CrawlerStopwatch;

    bool UserQuit = false;

    public WebCrawler(int crawlerThreads, int urlLimit)
    {
        CrawlerThreads = crawlerThreads;
        UrlLimit = urlLimit;

        ConfigureDirectories();
        
        TotalUrlsRequested = new ThreadSafeCounter();
        TotalUrlsProcessed = new ThreadSafeCounter();

        UrlFrontier = new BalancedUrlFrontier(CrawlerThreads);
        FrontierWrapper = new UrlFrontierWrapper(UrlFrontier);
        seenContentTracker = new SeenContentTracker();

        ProactiveLinksFinder = new ProactiveLinksFinder();
        ResponseLinkFinder = new ResponseLinkFinder();

        ResultsWarc = new ResultsWriter(CrawlerOptions.WarcDir);

    }

    private void ConfigureDirectories()
    {
        Directory.CreateDirectory(CrawlerOptions.WarcDir);
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
        => UrlFrontier.AddSeed(new GeminiUrl(url));

    public void DoCrawl()
    {
        RobotsChecker.Global.Crawler = this;
        ConfigureTimers();

        SpawnCrawlThreads();
        SpawnResultsWriter();

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

        Console.WriteLine("COMPLETE!");
        Console.WriteLine($"Elapsed: {CrawlerStopwatch.Elapsed}\tTotal Requested: {TotalUrlsRequested.Count}\tTotal Processed: {TotalUrlsProcessed.Count}\tRemaining: {UrlFrontier.Count}");
        FinalizeCrawl();
    }

    private void WritePendingResults()
    {
        do
        {
            Thread.Sleep(10000);
            if (KeepWorkersAlive)
            {
                ResultsWarc.Flush();
            }
        } while (KeepWorkersAlive);
    }

    private void FinalizeCrawl()
    {
        CrawlerStopwatch.Stop();
        ResultsWarc.Flush();
    }

    private void SpawnCrawlThreads()
    {
        for (int workerNum = 0; workerNum < CrawlerThreads; workerNum++)
        {
            var worker = new WebCrawlWorker(this, workerNum);
            var threadDelegate = new ThreadStart(worker.DoWork);
            var newThread = new Thread(threadDelegate);
            newThread.Name = $"Worker {workerNum}";
            newThread.Start();
        }
    }

    private void SpawnResultsWriter()
    {
        var threadDelegate = new ThreadStart(WritePendingResults);
        var newThread = new Thread(threadDelegate);
        newThread.Name = $"Results Writer";
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

    public void ProcessRobotsResponse(GeminiResponse response)
    {
        TotalUrlsRequested.Increment();
        ProcessRequestResponse(new UrlFrontierEntry
        {
            Url = response.RequestUrl,
            DepthFromSeed = 0,
            IsRobotsLimited = false
        }, response);
    }

    public void ProcessRequestResponse(UrlFrontierEntry entry, GeminiResponse response)
    { 
        //null means it was ignored by robots
        if (response != null)
        {
            if (response.ConnectStatus == ConnectStatus.Error)
            {
                errorLog.LogError(response.Meta, response.RequestUrl.NormalizedUrl);
            }

            ResultsWarc.AddToQueue(response);

            //if we haven't seen this content before, parse it for links and add them to the frontier
            if (!seenContentTracker.CheckAndRecord(response))
            {
                FrontierWrapper.AddUrls(entry.DepthFromSeed, ResponseLinkFinder.FindLinks(response));
            }
            //add proactive URLs
            FrontierWrapper.AddUrls(entry.DepthFromSeed, ProactiveLinksFinder.FindLinks(response), false);
        }
        TotalUrlsProcessed.Increment();
    }

    public UrlFrontierEntry GetUrl(int crawlerID = 0)
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
