using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;


using Gemini.Net;
using Kennedy.Blazer.Frontiers;
using Kennedy.Blazer.Logging;
using Kennedy.Blazer.Processors;
using Kennedy.Blazer.Protocols;
using Kennedy.Blazer.Utils;

namespace Kennedy.Blazer.Crawling;

public class Crawler : ICrawler
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
    List<IResponseProcessor> responseProcessors;
    SeenContentTracker seenContentTracker;

    System.Timers.Timer DiskStatusTimer;
    Stopwatch CrawlerStopwatch;

    public Crawler(int crawlerThreads, int urlLimit)
    {
        CrawlerThreads = crawlerThreads;
        UrlLimit = urlLimit;

        ConfigureDirectories();
        
        TotalUrlsRequested = new ThreadSafeCounter();
        TotalUrlsProcessed = new ThreadSafeCounter();

        UrlFrontier = new BalancedUrlFrontier(CrawlerThreads);
        FrontierWrapper = new UrlFrontierWrapper(UrlFrontier);

        seenContentTracker = new SeenContentTracker();

        responseProcessors = new List<IResponseProcessor>
        {
            new RedirectProcessor(FrontierWrapper),
            new GemtextProcessor(FrontierWrapper)
        };
    }

    private void ConfigureDirectories()
    {
        Directory.CreateDirectory(CrawlerOptions.OutputBase);
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

        int prevRequested = 0;

        do
        {
            Thread.Sleep(StatusIntervalScreen);

            int currRequested = TotalUrlsRequested.Count;
            string speed = ComputeSpeed((double)currRequested, (double)prevRequested, (double)StatusIntervalScreen);
            Console.WriteLine($"Elapsed: {CrawlerStopwatch.Elapsed}\tActive Workers: {WorkInFlight} Speed: {speed}\tTotal Requested: {currRequested}\tTotal Processed: {TotalUrlsProcessed.Count}\tRemaining: {UrlFrontier.Count}");
            prevRequested = TotalUrlsRequested.Count;

        } while (KeepWorkersAlive);
        CrawlerStopwatch.Stop();
        
        Console.WriteLine($"Complete! {CrawlerStopwatch.Elapsed.TotalSeconds}");
    }

    private void SpawnWorker(int workerNum)
    {
        var worker = new CrawlWorker(this, workerNum);

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
        StatusLogger logger = new StatusLogger(CrawlerOptions.OutputBase);
        logger.LogStatus(FrontierWrapper);
        logger.LogStatus(UrlFrontier);
    }

    public void ProcessRequestResponse(GeminiResponse resp, Exception ex)
    {
        //null means it was ignored by robots
        if (resp != null)
        {
            if (resp.ConnectStatus != ConnectStatus.Success)
            {
                var msg = ex?.Message ?? resp.Meta;
                errorLog.LogError(msg, resp.RequestUrl.NormalizedUrl);
            }
            else
            {
                ProcessResponse(resp);
            }
        }
        TotalUrlsProcessed.Increment();
    }

    private void ProcessResponse(GeminiResponse response)
    {
        if (!seenContentTracker.CheckAndRecord(response))
        {
            foreach (var processor in responseProcessors)
            {
                if (processor.CanProcessResponse(response))
                {
                    processor.ProcessResponse(response);
                }
            }
        }
    }

    public GeminiUrl GetUrl(int crawlerID = 0)
    {
        if (HitUrlLimit)
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
        => (!HitUrlLimit) ? (UrlFrontier.Count > 0) : false;

    /// <summary>
    /// Is there work being done
    /// </summary>
    public int WorkInFlight
        => TotalUrlsRequested.Count - TotalUrlsProcessed.Count;

    public bool KeepWorkersAlive
        => HasUrlsToFetch || (WorkInFlight > 0);

}
