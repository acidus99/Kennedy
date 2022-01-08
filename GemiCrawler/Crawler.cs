using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using System.Threading;

using Gemi.Net;
using GemiCrawler.DocumentIndex;
using GemiCrawler.DocumentStore;
using GemiCrawler.GemText;
using GemiCrawler.Modules;
using GemiCrawler.Utils;
using GemiCrawler.UrlFrontiers;

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

        ErrorLog errorLog;

        int stopAfterUrlCount { get; set; }

        ThreadSafeCounter totalUrlsRequested;

        BalancedUrlFrontier urlFrontier;

        ThreadSafeCounter workInFlight;

        SeenUrlModule seenUrlModule;
        SeenContentModule seenContentModule;
        RobotsFilterModule robotsModule;
        ExcludedUrlModule excludedUrlModule;
        DomainLimiterModule domainLimiter;

        DocIndex docIndex;
        DocStore docStore;

        List<AbstractModule> Modules;
        List<AbstractUrlModule> UrlModeles;

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

            InitOutputDirectories();

            urlFrontier = new BalancedUrlFrontier(crawlerThreadCount);

            workInFlight = new ThreadSafeCounter();
            totalUrlsRequested = new ThreadSafeCounter();
            crawlStopwatch = new Stopwatch();

            // init document repository and data bases
            docIndex = new DocIndex(Crawler.DataDirectory);
            docStore = new DocStore(Crawler.DataDirectory + "page-store/");

            //init errorlog
            errorLog = new ErrorLog(outputBase);

            Modules = new List<AbstractModule>();

            seenUrlModule = new SeenUrlModule();
            seenContentModule = new SeenContentModule();
            robotsModule = new RobotsFilterModule($"/{Crawler.DataDirectory}/robots/");
            excludedUrlModule = new ExcludedUrlModule($"/{Crawler.DataDirectory}/block-list.txt");
            domainLimiter = new DomainLimiterModule();

            SetupStatusLog(urlFrontier, "url-frontier");
            SetupStatusLog(seenUrlModule, "seen-urls");
            SetupStatusLog(seenContentModule, "seen-content");
            SetupStatusLog(robotsModule, "robots-filter");
            SetupStatusLog(excludedUrlModule, "url-filter");
            SetupStatusLog(domainLimiter, "domain-limiter");
            SetupStatusLog(this, "crawler");

            UrlModeles = new List<AbstractUrlModule>(Modules.Where(x => x is AbstractUrlModule).Select(x => (AbstractUrlModule)x).ToList());

            //Init Timers

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

        private void InitOutputDirectories()
        {
            Directory.CreateDirectory(outputBase);
            Directory.CreateDirectory(SnapshotDirectory);
        }

        private void SnapshotTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;

            //urlFrontier.SaveSnapshot($"{SnapshotDirectory}{now.ToString("dd (hhmmss)")}-url-frontiers.txt");
        }

        private void StatusTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var module in Modules)
            {
                module.OutputStatus();
            }
        }

        private void SetupStatusLog(AbstractModule module, string logName)
        {
            module.LogFilename = $"{outputBase}log-{logName}.txt";
            Modules.Add(module);
        }

        private void FinalizeCrawl()
        {
            errorLog.Close();
            docIndex.Close();
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

            urlFrontier.SaveSnapshot($"{DataDirectory}remaining-frontier.txt");

            Console.WriteLine($"Complete! {crawlStopwatch.Elapsed.TotalSeconds}");
            FinalizeCrawl();
        }

        public GemiUrl GetNextUrl(int crawlerID = 0)
        {
            if (HitUrlLimit)
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
            foreach (var urlModule in UrlModeles)
            {
                if (!urlModule.IsUrlAllowed(url))
                {
                    return;
                }
            }
            urlFrontier.AddUrl(url);
        }

        public void ProcessResult(GemiUrl url, GemiResponse resp, Exception ex)
        {
            processedCounter.Increment();
            if (resp.ConnectStatus != ConnectStatus.Success)
            {
                errorLog.LogError(ex, url.NormalizedUrl);
            }
            
            //Modules
            if (!seenContentModule.CheckAndRecord(resp))
            {
                var foundLinks = LinkFinder.ExtractLinks(resp);
                foundLinks.ForEach(x => ProcessProspectiveUrl(x.Url));
                bool savedBody = docStore.StoreDocument(resp);
                docIndex.StoreMetaData(url, resp, foundLinks.Count, savedBody);
                docIndex.StoreLinks(url, foundLinks);
                
            }
            
            //note the work is complete
            workInFlight.Decrement();
        }

        protected override string GetStatusMesssage()
            => $"Elapsed: {crawlStopwatch.Elapsed}\tTotal Requested: {totalUrlsRequested.Count}\tTotal Processed: {processedCounter.Count}";

        public bool HitUrlLimit
            => (totalUrlsRequested.Count >= stopAfterUrlCount);

        /// <summary>
        /// Is there pending work in our queue?
        /// </summary>
        public bool HasUrlsToFetch
        {
            get
            {
                if (HitUrlLimit)
                {
                    return false;
                }
                return (urlFrontier.GetCount() > 0);
            }
        }

        public void LoadPreviousResults()
        {
            //load frontier with URLs
            urlFrontier.PopulateFromSnapshot($"{DataDirectory}remaining-frontier.txt");

            //populate our seen URLs list with everything that's in the frontier...
            seenUrlModule.PopulateWithSeenIDs(urlFrontier.GetSnapshot().Select(x=>x.DocID).ToList());
            //... and what we have already saved in the database
            seenUrlModule.PopulateWithSeenIDs(docIndex.GetDocIDs());

            //load previous known body hashes
            seenContentModule.PopulateWithSeenHashes(docIndex.GetBodyHashes());
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