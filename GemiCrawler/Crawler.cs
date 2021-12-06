using System;
using System.IO;
using Gemi.Net;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using GemiCrawler.Modules;
using System.Linq;
using GemiCrawler.Utils;
using GemiCrawler.UrlFrontiers;
using System.Timers;

namespace GemiCrawler
{
    public class Crawler : AbstractModule, ICrawler
    {

        const int SnapshotInterval = 2 * StatusIntervalDisk;
        const int StatusIntervalDisk = 60000;
        const int StatusIntervalScreen = 5000;

        public const string DataDirectory = "/Users/billy/Code/gemini-play/crawler-data-files/";

        readonly string outputBase = $"/Users/billy/Code/gemini-play/crawl-out/{DateTime.Now.ToString("yyyy-MM-dd (hhmmss)")}/";

        int crawlerThreadCount;

        ThreadedFileWriter errorOut;
        ThreadedFileWriter logOut;

        int stopAfterUrlCount { get; set; }

        ThreadSafeCounter totalUrlsRequested;

        BalancedUrlFrontier urlFrontier;
        DocumentStore docStore;

        ThreadSafeCounter workInFlight;

        SeenUrlModule seenUrlModule;
        SeenContentModule seenContentModule;
        RobotsFilterModule robotsModule;
        ExcludedUrlModule excludedUrlModule;

        Stopwatch crawlStopwatch;

        System.Timers.Timer statusTimer;
        System.Timers.Timer snapshotTimer;

        private string SnapshotDirectory
            => $"{outputBase}snapshots/";

        public Crawler(int threadCount, int stopAfterCount)
            : base("Crawler")
        {
            crawlerThreadCount = threadCount;

            stopAfterUrlCount = stopAfterCount;

            urlFrontier = new BalancedUrlFrontier(crawlerThreadCount);

            workInFlight = new ThreadSafeCounter();
            totalUrlsRequested = new ThreadSafeCounter();

            crawlStopwatch = new Stopwatch();
            

            seenUrlModule = new SeenUrlModule();
            seenContentModule = new SeenContentModule();
            robotsModule = new RobotsFilterModule($"/{Crawler.DataDirectory}/robots/");
            excludedUrlModule = new ExcludedUrlModule($"/{Crawler.DataDirectory}/block-list.txt");

            Directory.CreateDirectory(outputBase);
            Directory.CreateDirectory(SnapshotDirectory);
            docStore = new DocumentStore(outputBase + "page-store/");
            errorOut = new ThreadedFileWriter(outputBase + "errors.txt", 1);
            logOut = new ThreadedFileWriter(outputBase + "log-responses.tsv", 20);


            statusTimer = new System.Timers.Timer(StatusIntervalDisk)
            {
                Enabled = true,
                AutoReset = true
            };
            statusTimer.Elapsed += StatusTimer_Elapsed;

            snapshotTimer = new System.Timers.Timer(SnapshotInterval)
            {
                Enabled = true,
                AutoReset = true
            };
            snapshotTimer.Elapsed += SnapshotTimer_Elapsed;
        }

        private void SnapshotTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;

            //urlFrontier.SaveSnapshot($"{SnapshotDirectory}{now.ToString("dd (hhmmss)")}-url-frontiers.txt");
        }

        private void StatusTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OutputStatus($"{outputBase}log-crawler.txt");
            urlFrontier.OutputStatus($"{outputBase}log-url-frontier.txt");
            robotsModule.OutputStatus($"{outputBase}log-robots.txt");
            seenUrlModule.OutputStatus($"{outputBase}log-seen-urls.txt");
            seenContentModule.OutputStatus($"{outputBase}log-seen-content.txt");
            docStore.OutputStatus($"{outputBase}log-doc-store.txt");
            excludedUrlModule.OutputStatus($"{outputBase}log-blocked-urls.txt");
        }

        #region Log Stuff

        private void LogError(Exception ex, GemiUrl url)
        {
            var msg = $"EXCEPTION {ex.Message} on '{url}'";
            errorOut.WriteLine($"{DateTime.Now}\t{msg}");
        }

        private void LogWarn(string what)
        {
            var msg = $"WARNING! {what}";
            errorOut.WriteLine($"{DateTime.Now}\t{msg}");
        }


        private void CloseLogs()
        {
            errorOut.Close();
            logOut.Close();
        }

        private void LogPage(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks)
        {
            var msg = $"{resp.StatusCode}\t{resp.MimeType}\t{url}\t{resp.BodySize}\t{resp.ConnectTime}\t{resp.DownloadTime}\t{foundLinks.Count}";
            logOut.WriteLine(msg);
        }

        #endregion

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

        public void DoCrawl()
        {
            crawlStopwatch.Start();
            statusTimer.Start();
            for (int i = 0; i < crawlerThreadCount; i++)
            {
                SpawnWorker(i);
            }

            int prevRequested = 0;

            do
            {
                Thread.Sleep(StatusIntervalScreen);
                
                int currRequested = totalUrlsRequested.Count;
                string speed = ComputeSpeed((double)currRequested, (double)prevRequested, (double)StatusIntervalScreen);
                Console.WriteLine($"Elapsed: {crawlStopwatch.Elapsed}\tSpeed: {speed}\tTotal Requested: {currRequested}\tTotal Processed: {processedCounter.Count}");
                prevRequested = totalUrlsRequested.Count;

            } while (KeepWorkersAlive);
            crawlStopwatch.Stop();

            Console.WriteLine($"Complete! {crawlStopwatch.Elapsed.TotalSeconds}");
            CloseLogs();
            int x = 4;
        }

        public GemiUrl GetNextUrl(int crawlerID = 0)
        {
            if(HitUrlLimit)
            {
                return null;
            }

            var url = urlFrontier.GetUrl(crawlerID);
            if (url != null)
            {
                workInFlight.Increment();
                totalUrlsRequested.Increment();
                
            }
            return url;
        }

        public void AddSeed(string url)
            => ProcessProspectiveUrl(new GemiUrl(url));

        private void ProcessProspectiveUrl(GemiUrl url)
        {
            //Modules that process URLs
            if(!robotsModule.IsUrlAllowed(url))
            {
                return;
            }

            if(!excludedUrlModule.IsUrlAllowed(url))
            {
                return;
            }

            if(!seenUrlModule.CheckAndRecord(url))
            {
                urlFrontier.AddUrl(url);
            }
        }

        private void ProcessProspectiveUrls(List<GemiUrl> urls)
        {
            urls.ForEach(x => ProcessProspectiveUrl(x));
        }

        public void ProcessResult(GemiUrl url, GemiResponse resp, Exception ex)
        {

            processedCounter.Increment();
            if (resp.ConnectStatus != ConnectStatus.Success)
            {
                LogError(ex, url);
            }
            else if (resp != null)
            {
                //Modules
                if (!seenContentModule.CheckAndRecord(resp))
                {
                    var foundLinks = LinkFinder.ExtractUrls(url, resp);

                    ProcessProspectiveUrls(foundLinks);
                    StoreStatsAndDocument(url, resp, foundLinks);
                }
            }
            //note the work is complete
            workInFlight.Decrement();
        }

        /// <summary>
        /// Saves stats and documents or content about this result
        /// for later processing...
        /// </summary>
        private void StoreStatsAndDocument(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks)
        {
            LogPage(url, resp, foundLinks);
            if (!docStore.Store(url, resp))
            {
                LogWarn($"Could not save document for '{url}' to disk");
            }
        }

        public override void OutputStatus(string outputFile)
        {
            File.AppendAllText(outputFile, CreateLogLine($"Elapsed: {crawlStopwatch.Elapsed}\tTotal Requested: {totalUrlsRequested.Count}\tTotal Processed: {processedCounter.Count}\n"));
        }

        public bool HitUrlLimit
            => (totalUrlsRequested.Count >= stopAfterUrlCount);

        /// <summary>
        /// Is there pending work in our queue?
        /// </summary>
        public bool HasUrlsToFetch
        {
            get {
                if (HitUrlLimit)
                {
                    return false;
                }
                return (urlFrontier.GetCount() > 0);
            }
        }

        /// <summary>
        /// Is there work being done
        /// </summary>
        public bool WorkInFlight
            => (workInFlight.Count > 0);

        public bool KeepWorkersAlive
            => HasUrlsToFetch || WorkInFlight;

    }
}