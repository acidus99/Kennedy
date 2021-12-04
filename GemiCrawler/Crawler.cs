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

namespace GemiCrawler
{
    public class Crawler : ICrawler
    {

        public const string DataDirectory = "/Users/billy/Code/gemini-play/crawler-data-files/";

        readonly string outputBase = $"/Users/billy/Code/gemini-play/crawl-out/{DateTime.Now.ToString("yyyy-MM-dd (hhmmss)")}/";

        int crawlerThreadCount;

        ThreadedFileWriter errorOut;
        ThreadedFileWriter logOut;

        int stopAfterUrlCount { get; set; }

        ThreadSafeCounter totalUrlsRequested;

        IUrlFrontier urlFrontier;
        DocumentStore docStore;

        ThreadSafeCounter workInFlight;

        SeenUrlModule seenUrlModule;
        SeenContentModule seenContentModule;
        RobotsFilterModule robotsModule;

        public Crawler(int threadCount, int stopAfterCount)
        {
            crawlerThreadCount = threadCount;

            stopAfterUrlCount = stopAfterCount;

            urlFrontier = new BalancedUrlFrontier(crawlerThreadCount);

            workInFlight = new ThreadSafeCounter();
            totalUrlsRequested = new ThreadSafeCounter();

            seenUrlModule = new SeenUrlModule();
            seenContentModule = new SeenContentModule();
            robotsModule = new RobotsFilterModule($"/{Crawler.DataDirectory}/robots/");

            Directory.CreateDirectory(outputBase);
            docStore = new DocumentStore(outputBase + "page-store/");
            errorOut = new ThreadedFileWriter(outputBase + "errors.txt", 1);
            logOut = new ThreadedFileWriter(outputBase + "log.tsv", 20);
        }

        #region Log Stuff

        private void LogError(Exception ex, GemiUrl url)
        {
            var msg = $"EXCEPTION {ex.Message} on '{url}'";
            Console.WriteLine(msg);
            errorOut.WriteLine($"{DateTime.Now}\t{msg}");

            msg = $"XX\t{ex.Message}\t{url}\t0\t0";
            logOut.WriteLine(msg);

        }

        private void LogWarn(string what)
        {
            var msg = $"WARNING! {what}";
            Console.WriteLine(msg);
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

        public void DoCrawl()
        {
            Stopwatch watcher = new Stopwatch();

            watcher.Start();

            for (int i = 0; i < crawlerThreadCount; i++)
            {
                SpawnWorker(i);
            }

            do
            {
                Thread.Sleep(500);
                Console.WriteLine($"Main Thread Sleeping\tTotal Requested: {totalUrlsRequested.Count}");

            } while (KeepWorkersAlive);
            watcher.Stop();

            Console.WriteLine($"Complete! {watcher.Elapsed.TotalSeconds}");
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