using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Gemi.Net;
using GemiCrawler.Modules;

namespace GemiCrawler.UrlFrontiers
{
    /// <summary>
    /// Manages our queue of URLs to crawl
    /// </summary>
    public class BalancedUrlFrontier : AbstractModule
    {
        object locker;

        /// <summary>
        /// our queue of URLs to crawl
        /// </summary>
        PriorityQueue [] queues;

        int totalWorkerThreads;

        public BalancedUrlFrontier(int totalWorkers)
            : base("URL-FRONTIER")
        {
            locker = new object();
            totalWorkerThreads = totalWorkers;

            queues = new PriorityQueue[totalWorkerThreads];
            for(int i = 0; i< totalWorkerThreads; i++)
            {
                queues[i] = new PriorityQueue();
            }
        }

        private int queueForUrl(GemiUrl url)
        {
            return Math.Abs(url.Authority.GetHashCode()) % totalWorkerThreads;
        }

        public void AddUrl(GemiUrl url)
        {
            int queueID = queueForUrl(url);
            queues[queueID].AddUrl(url);
        }

        public int GetCount()
        {
            int totalCount = 0;
            for(int i=0;i<totalWorkerThreads; i++)
            {
                totalCount += queues[i].GetCount();
            }
            return totalCount;
        }

        public GemiUrl GetUrl(int crawlerID = 0)
            => queues[crawlerID].GetUrl();

        protected override string GetStatusMesssage()
            => $"Total Queue Size: {GetCount()}";

        public List<GemiUrl> GetSnapshot()
        {
            var ret = new List<GemiUrl>();
            foreach(var queue in queues)
            {
                ret.AddRange(queue.GetSnapshot());
            }
            return ret;
        }

        public void SaveSnapshot(string filename)
        {
            //Anything left in the Frontier?
            var remaining = GetSnapshot();
            File.WriteAllLines(filename, remaining.Select(x=>x.NormalizedUrl));
        }

        public void PopulateFromSnapshot(string filename)
        {
            var urls = File.ReadAllLines(filename).Select(x => new GemiUrl(x));
            foreach (var url in urls)
            {
                AddUrl(url);
            }
        }
    }
}
