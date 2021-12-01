using System;
using System.IO;
using Gemi.Net;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace GemiCrawler
{
    public class Crawler : ICrawler
    {
        readonly string outputBase = $"/Users/billy/Code/gemini-play/crawl-out/{DateTime.Now.ToString("yyyy-MM-dd (hhmmss)")}/";

        const int threadCount = 16;

        ThreadedFileWriter errorOut;
        ThreadedFileWriter logOut;

        int stopAfterUrls = int.MaxValue;

        ThreadSafeCounter totalUrlsRequested;

        CrawlQueue queue;
        DocumentStore docStore;

        ThreadSafeCounter workInFlight;

        public Crawler()
        {
            stopAfterUrls = 50000;

            queue = new CrawlQueue();

            workInFlight = new ThreadSafeCounter();
            totalUrlsRequested = new ThreadSafeCounter();

            Directory.CreateDirectory(outputBase);
            docStore = new DocumentStore(outputBase + "page-store/");
            errorOut = new ThreadedFileWriter(outputBase + "errors.txt", 1);
            logOut = new ThreadedFileWriter(outputBase + "log.tsv", 20);
        }

        public void AddSeed(string url)
        {
            queue.EnqueueUrl(new GemiUrl(url));
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
            var worker = new CrawlWorker(this);

            var threadDelegate = new ThreadStart(worker.DoWork);
            var newThread = new Thread(threadDelegate);
            newThread.Name = $"Worker {workerNum}";
            newThread.Start();
        }

        public void DoCrawl()
        {
            Stopwatch watcher = new Stopwatch();

            watcher.Start();

            for (int i = 1; i <= threadCount; i++)
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

        public GemiUrl GetNextUrl()
        {
            if(HitUrlLimit)
            {
                return null;
            }

            var url = queue.DequeueUrl();
            if (url != null)
            {
                totalUrlsRequested.Increment();
                workInFlight.Increment();
            }
            return url;
        }

        public void ProcessResult(GemiUrl url, GemiResponse resp, Exception ex)
        {
            if (resp.ConnectStatus != ConnectStatus.Success)
            {
                LogError(ex, url);
            }
            else if (resp != null)
            {
                var foundLinks = LinkFinder.ExtractUrls(url, resp);
                queue.EnqueueUrls(foundLinks);
                LogPage(url, resp, foundLinks);
                if (!docStore.Store(url, resp))
                {
                    LogWarn($"Could not save document for '{url}' to disk");
                }

            }
            //note the work is complete
            workInFlight.Decrement();
        }


        public bool HitUrlLimit
            => (totalUrlsRequested.Count >= stopAfterUrls);

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
                return (queue.Count > 0);
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