﻿using System.Diagnostics;
using Gemini.Net;
using Kennedy.Crawler.Frontiers;
using Kennedy.Crawler.Logging;
using Kennedy.Crawler.Protocols;
using Kennedy.Crawler.Utils;
using Kennedy.Data;
using Kennedy.Data.Parsers;

namespace Kennedy.Crawler.Crawling;

public class WebCrawler : IWebCrawler
{
    const int StatusIntervalScreen = 5000;

    int CrawlerThreads;
    ThreadSafeCounter TotalUrlsRequested;
    ThreadSafeCounter TotalUrlsProcessed;

    RejectedUrlLogger rejectionLogger;
    RemainingUrlLogger remainingUrlLogger;
    ResponseLogger responseLogger;

    int UrlLimit;

    bool HitUrlLimit
        => (TotalUrlsRequested.Count >= UrlLimit);

    ResponseParser responseParser;

    IUrlFrontier UrlFrontier;
    UrlFrontierWrapper FrontierWrapper;

    ResponseParser responseParser;

    ILinksFinder ResponseLinkFinder;
    ILinksFinder ProactiveLinksFinder;

    SeenContentTracker seenContentTracker;

    ResultsWriter ResultsWriter;

    Stopwatch CrawlerStopwatch;

    bool UserQuit;
    string StopFilePath;

    public bool LimitCrawlToSeeds
    {
        get { return FrontierWrapper.LimitCrawlToSeeds; }
        set { FrontierWrapper.LimitCrawlToSeeds = value; }
    }

    public WebCrawler(int crawlerThreads, int urlLimit)
    {
        CrawlerThreads = crawlerThreads;
        UrlLimit = urlLimit;

        ConfigureDirectories();
        LanguageDetector.ConfigFileDirectory = CrawlerOptions.ConfigDir;

        rejectionLogger = new RejectedUrlLogger(CrawlerOptions.RejectionsLog);
        remainingUrlLogger = new RemainingUrlLogger(CrawlerOptions.RemainingUrlsLog);
        responseLogger = new ResponseLogger(CrawlerOptions.ResponsesLog);

        TotalUrlsRequested = new ThreadSafeCounter();
        TotalUrlsProcessed = new ThreadSafeCounter();

        UrlFrontier = new BalancedUrlFrontier(CrawlerThreads);
        FrontierWrapper = new UrlFrontierWrapper(UrlFrontier, rejectionLogger);
        seenContentTracker = new SeenContentTracker();

        ProactiveLinksFinder = new ProactiveLinksFinder();

        responseParser = new ResponseParser();
        ResultsWriter = new ResultsWriter(CrawlerOptions.WarcDir, CrawlerOptions.DocumentIndex);

        CrawlerStopwatch = new Stopwatch();

        UserQuit = false;
        StopFilePath = GetStopFilePath();
    }

    private string GetStopFilePath()
    {
        var info = new DirectoryInfo(".");
        return info.FullName + "/stop";
    }

    private void ConfigureDirectories()
    {
        Directory.CreateDirectory(CrawlerOptions.WarcDir);
        Directory.CreateDirectory(CrawlerOptions.Logs);
    }

    public bool AddSeed(string url)
    {
        try
        {
            return FrontierWrapper.AddSeed(new GeminiUrl(url));
        }
        catch (UriFormatException)
        {
            //some entries in the seeds file might be invalid URLs, just ignore them
            Console.WriteLine($"Skipping invalid seed URL {url}");
        }
        return false;
    }

    public void AddSeedsFromFile(string filename)
    {
        using (StreamWriter fout = new StreamWriter(CrawlerOptions.SeedLog))
        {
            using (StreamReader fin = new StreamReader(filename))
            {
                string? line;
                while ((line = fin.ReadLine()) != null)
                {
                    bool result = AddSeed(line);
                    if(result)
                    {
                        fout.WriteLine($"ALLOWED\t{line}");
                    } else
                    {
                        fout.WriteLine($"DENIED\t{line}");
                    }
                }
            }
        }
    }


    public void AddUrlsFromWebDB()
    {
        InitialUrlSelector selector = new InitialUrlSelector();
        while (selector.MoveNext())
        {
            FrontierWrapper.AddInitialUrl(selector.Current);
        }
    }

    public void DoCrawl()
    {
        RobotsChecker.Global.Crawler = this;
        CrawlerStopwatch.Start();

        SpawnCrawlThreads();
        SpawnResultsWriter();

        int prevRequested = 0;
        do
        {
            CheckForQuit();
            Thread.Sleep(StatusIntervalScreen);

            int currRequested = TotalUrlsRequested.Count;
            string speed = ComputeSpeed((double)currRequested, (double)prevRequested, (double)StatusIntervalScreen);
            Console.WriteLine($"Elapsed: {CrawlerStopwatch.Elapsed}\tActive Workers: {WorkInFlight} Speed: {speed}\tTotal Requested: {currRequested}\tTotal Processed: {TotalUrlsProcessed.Count}\tRemaining: {UrlFrontier.Count}");
            prevRequested = TotalUrlsRequested.Count;

        } while (KeepWorkersAlive);

        CrawlerStopwatch.Stop();
        Console.WriteLine("COMPLETE!");
        Console.WriteLine($"Elapsed: {CrawlerStopwatch.Elapsed}\tTotal Requested: {TotalUrlsRequested.Count}\tTotal Processed: {TotalUrlsProcessed.Count}\tRemaining: {UrlFrontier.Count}");
        FinalizeCrawl();
    }

    private void CheckForQuit()
    {
        //CheckForInteractiveQuit();
        CheckForFileQuit();
    }

    //private void CheckForInteractiveQuit()
    //{
    //    if (Console.In is StreamReader)
    //    {
    //        if (Console.KeyAvailable)
    //        {
    //            Console.WriteLine("stop? type 'quit'");
    //            Console.ReadKey(true);
    //            if (Console.ReadLine() == "quit")
    //            {
    //                Console.WriteLine("quiting...");
    //                UserQuit = true;
    //            }
    //            else
    //            {
    //                Console.WriteLine("resuming");
    //            }
    //        }
    //    }
    //}

    private void CheckForFileQuit()
    {
        if (File.Exists(StopFilePath))
        {
            UserQuit = true;
            File.Delete(StopFilePath);
            Console.WriteLine("Stop file detected. Stopping crawl");
        }
    }

    private void WritePendingResults()
    {
        do
        {
            Thread.Sleep(10000);
            if (KeepWorkersAlive)
            {
                ResultsWriter.Flush();
            }
        } while (KeepWorkersAlive);
    }

    private void FinalizeCrawl()
    {
        //flush and close our logs
        rejectionLogger.Close();
        remainingUrlLogger.Close();
        responseLogger.Close();

        //flush and close our results
        ResultsWriter.Close();
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
        return $"{requestSec:00.0} req / sec";
    }

    public void LogRejectedUrl(GeminiUrl url, string rejectionType, string specificRule = "")
        => rejectionLogger.LogRejection(url, rejectionType, specificRule);

    public void ProcessRobotsResponse(GeminiResponse response)
    {
        TotalUrlsRequested.Increment();
        responseLogger.LogUrlResponse(response);

        var parsedResponse = responseParser.Parse(response);
        ResultsWriter.AddResponse(parsedResponse);
        TotalUrlsProcessed.Increment();
    }

    public void ProcessRequestResponse(UrlFrontierEntry entry, GeminiResponse response)
        => ProcessRequestResponseHelper(entry, response, SkippedReason.NotSkipped);

    public void ProcessSkippedRequest(UrlFrontierEntry entry, SkippedReason reason)
        => ProcessRequestResponseHelper(entry, null, reason);

    private void ProcessRequestResponseHelper(UrlFrontierEntry entry, GeminiResponse? response, SkippedReason reason)
    {
        if (reason == SkippedReason.NotSkipped)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response), "response was null while skip reason was not skipped!");
            }

            ParsedResponse parsedResponse = responseParser.Parse(response);

            responseLogger.LogUrlResponse(response);
            ResultsWriter.AddResponse(parsedResponse);

            //record this url as now seen, and see if we have seen it before
            bool seenBefore = seenContentTracker.CheckAndRecord(response);

            //Don't parse proactive links for URLs. This can lead to bugs
            //e.g. a broken security.txt file that returns a gemtext doc, with relative links, creating a spider trap
            if (!seenBefore && !entry.IsProactive)
            {
                FrontierWrapper.AddUrls(entry.DepthFromSeed, parsedResponse.Links);
            }
            //add proactive URLs
            FrontierWrapper.AddUrls(entry.DepthFromSeed, ProactiveLinksFinder.FindLinks(response), false);
        }
        else
        {
            ResultsWriter.AddSkippedUrlResponse(entry.Url, reason);
        }

        TotalUrlsProcessed.Increment();
    }

    public UrlFrontierEntry? GetUrl(int crawlerID = 0)
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

    public void LogRemainingUrl(UrlFrontierEntry entry)
        => remainingUrlLogger.LogRemainingUrl(entry);

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