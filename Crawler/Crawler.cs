using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;

using Gemini.Net;
using Kennedy.Crawler.DocumentParsers;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Kennedy.Crawler.GemText;
using Kennedy.Crawler.Modules;
using Kennedy.Crawler.Utils;
using Kennedy.Crawler.UrlFrontiers;

namespace Kennedy.Crawler
{
    public class Crawler : AbstractModule, ICrawler
    {

        const int SnapshotInterval = 2 * StatusIntervalDisk;
        const int StatusIntervalDisk = 60000;
        const int StatusIntervalScreen = 5000;

        readonly string outputBase = $"{CrawlerOptions.DataDirectory}/logs/{DateTime.Now.ToString("yyyy-MM-dd (hhmmss)")}/";

        int crawlerThreadCount;

        ErrorLog errorLog;

        int stopAfterUrlCount { get; set; }

        ThreadSafeCounter totalUrlsRequested;

        BalancedUrlFrontier urlFrontier;
        Bag<string> HitsForDomain = new Bag<string>();

        ThreadSafeCounter workInFlight;

        SeenUrlModule seenUrlModule;
        SeenContentModule seenContentModule;
        RobotsFilterModule robotsModule;
        ExcludedUrlModule excludedUrlModule;
        DomainLimiterModule domainLimiter;

        DocumentIndex docIndex;
        DocumentStore docStore;
        FullTextSearchEngine ftsEngine;

        DocumentParser docParser;

        List<AbstractModule> Modules;
        List<AbstractUrlModule> UrlModeles;

        Stopwatch crawlStopwatch;

        System.Timers.Timer statusTimer;
        System.Timers.Timer snapshotTimer;

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
            HitsForDomain = new Bag<string>();

            // init document repository and data bases
            docIndex = new DocumentIndex(CrawlerOptions.DataDirectory);
            ftsEngine = new FullTextSearchEngine(CrawlerOptions.DataDirectory);
            docStore = new DocumentStore(CrawlerOptions.DataDirectory + "page-store/");

            docParser = new DocumentParser();

            //init errorlog
            errorLog = new ErrorLog(outputBase);

            Modules = new List<AbstractModule>();

            seenUrlModule = new SeenUrlModule();
            seenContentModule = new SeenContentModule();
            robotsModule = new RobotsFilterModule($"/{CrawlerOptions.DataDirectory}/robots/");
            excludedUrlModule = new ExcludedUrlModule($"/{CrawlerOptions.DataDirectory}/block-list.txt");
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

            urlFrontier.SaveSnapshot($"{CrawlerOptions.DataDirectory}remaining-frontier.txt");

            Console.WriteLine($"Complete! {crawlStopwatch.Elapsed.TotalSeconds}");
            FinalizeCrawl();
        }

        public GeminiUrl GetNextUrl(int crawlerID = 0)
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

        public void AddSeedFile(string capsulesFile)
        {
            foreach (var authority in File.ReadAllLines(capsulesFile))
            {
                AddSeed($"gemini://{authority}/");
            }
        }

        public void PreheatDns(string capsulesFile)
        {
            var capsules = File.ReadAllLines(capsulesFile);
            DnsCache dnsCache = new DnsCache();
            Console.WriteLine("Warming DNS");
            Parallel.ForEach(capsules, capsule =>
            {
                if (capsule.IndexOf(":") > 0)
                {
                    capsule = capsule.Substring(0, capsule.IndexOf(":"));
                }
                dnsCache.GetLookup(capsule);
                urlFrontier.DnsCache = dnsCache;
            }); //close method invocation
        }

        public void AddSeed(string url)
        {
            try
            {
                ProcessProspectiveUrl(new GeminiUrl(url));
            }
            catch (Exception) { }
        }

        private void ProcessProspectiveUrl(GeminiUrl url)
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

        public void ProcessResult(GeminiUrl url, GeminiResponse resp, Exception ex)
        {
            processedCounter.Increment();
            if (resp.ConnectStatus != ConnectStatus.Success)
            {
                errorLog.LogError(resp.Meta, url.NormalizedUrl);
            }
            
            //Modules
            if (!seenContentModule.CheckAndRecord(resp))
            {
                //parse it
                DocumentMetadata metaData = docParser.ParseDocument(resp);
                //act on the links
                metaData.Links.ForEach(x => ProcessProspectiveUrl(x.Url));

                var dbDocID = SaveResponse(resp, metaData);
                IndexDocument(dbDocID, metaData);
            }
            
            //note the work is complete
            workInFlight.Decrement();
        }

        private void IndexDocument(long dbDocID, DocumentMetadata metaData)
        {
            if (metaData.IsIndexable)
            {
                ftsEngine.AddResponseToIndex(dbDocID, metaData.Title, metaData.FilteredBody);
            }
        }

        private long SaveResponse(GeminiResponse resp, DocumentMetadata metaData)
        {
            //store it in our doc storeage
            bool savedBody = docStore.StoreDocument(resp);

            //store in in the doc index (inserting or updating as appropriate
            long dbDocID = docIndex.StoreMetaData(resp, metaData.Title, metaData.Links.Count, savedBody, metaData.Language, metaData.LineCount);
            docIndex.StoreLinks(resp.RequestUrl, metaData.Links);
            SaveDomainStats(resp.RequestUrl);
            return dbDocID;
        }

        private void SaveDomainStats(GeminiUrl url)
        {
            var count = HitsForDomain.Add(url.Authority);
            if (count == 1)
            {
                AnalyzeDomain(url);
            }
        }

        private void AnalyzeDomain(GeminiUrl url)
        {
            Console.WriteLine("analysing domain: " + url.Hostname);
            DomainAnalyzer analyzer = new DomainAnalyzer(url.Hostname, url.Port);
            analyzer.QueryDomain(urlFrontier.DnsCache);

            using (var db = docIndex.GetContext())
            {
                db.DomainEntries.Add(
                    new StoredDomainsEntry
                    {
                        Domain = url.Hostname,
                        Port = url.Port,

                        IsReachable = analyzer.IsReachable,
                        ErrorMessage = analyzer.ErrorMessage,

                        HasFaviconTxt = analyzer.HasValidFavionTxt,
                        HasRobotsTxt = analyzer.HasValidRobotsTxt,
                        HasSecurityTxt = analyzer.HasValidSecurityTxt,

                        FaviconTxt = analyzer.FaviconTxt,
                        RobotsTxt = analyzer.RobotsTxt,
                        SecurityTxt = analyzer.SecurityTxt
                    });
                db.SaveChanges();
            }
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

        //public void LoadPreviousResults()
        //{
        //    //load frontier with URLs
        //    urlFrontier.PopulateFromSnapshot($"{DataDirectory}remaining-frontier.txt");

        //    //populate our seen URLs list with everything that's in the frontier...
        //    seenUrlModule.PopulateWithSeenIDs(urlFrontier.GetSnapshot().Select(x=>x.DocID).ToList());
        //    //... and what we have already saved in the database
        //    seenUrlModule.PopulateWithSeenIDs(docIndex.GetDocIDs());

        //    //load previous known body hashes
        //    seenContentModule.PopulateWithSeenHashes(docIndex.GetBodyHashes());
        //}


        /// <summary>
        /// Is there work being done
        /// </summary>
        public bool WorkInFlight
            => (workInFlight.Count > 0);

        public bool KeepWorkersAlive
            => HasUrlsToFetch || WorkInFlight;

    }
}