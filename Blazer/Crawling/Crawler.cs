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

public class Crawler
{
    const int StatusIntervalDisk = 60000;
    const int StatusIntervalScreen = 5000;

    /// <summary>
    /// how long should we wait between requests
    /// </summary>
    const int delayMs = 350;

    int CrawlerThreads;

    ErrorLog errorLog;

    IUrlFrontier UrlFrontier;
    UrlFrontierWrapper FrontierWrapper;
    List<IResponseProcessor> responseProcessors;
    SeenContentTracker seenContentTracker;

    System.Timers.Timer DiskStatusTimer;
    System.Timers.Timer ScreenStatusTimer;
    Stopwatch CrawlerStopwatch;

    public Crawler(int crawlerThreads)
    {
        ConfigureDirectories();

        CrawlerThreads = crawlerThreads;

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

        ScreenStatusTimer = new System.Timers.Timer(StatusIntervalScreen)
        {
            Enabled = true,
            AutoReset = true,
        };
        ScreenStatusTimer.Elapsed += LogStatusToScreen;

        DiskStatusTimer.Start();
        ScreenStatusTimer.Start();
    }

    public void AddSeed(string url)
        => UrlFrontier.AddUrl(new GeminiUrl(url));

    public void DoCrawl()
    {
        ConfigureTimers();

        var requestor = new GeminiProtocolHandler();

        GeminiUrl url = null;
        do
        {
            url = UrlFrontier.GetUrl(0);
            if (url != null)
            {
                var resp = requestor.Request(url);
                //null means it was ignored by robots
                if (resp != null)
                {
                    if (resp.ConnectStatus != ConnectStatus.Success)
                    {
                        var msg = requestor.LastException?.Message ?? resp.Meta;
                        errorLog.LogError(msg, url.NormalizedUrl);
                    }
                    else
                    {
                        ProcessResponse(resp);
                    }
                }
            }

            Thread.Sleep(delayMs);

        } while (url != null);
        Console.WriteLine("Complete!");
    }

    private void LogStatusToDisk(object? sender, System.Timers.ElapsedEventArgs e)
    {
        StatusLogger logger = new StatusLogger(CrawlerOptions.OutputBase);
        logger.LogStatus(FrontierWrapper);
        logger.LogStatus(UrlFrontier);
    }

    private void LogStatusToScreen(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Console.WriteLine($"Elapsed: {CrawlerStopwatch.Elapsed}\tTotal Added: {UrlFrontier.Total}\tRemaining: {UrlFrontier.Count}");
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
}
