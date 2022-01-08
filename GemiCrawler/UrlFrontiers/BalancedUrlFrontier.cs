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

        DnsCache dnsCache;
        int totalWorkerThreads;

        public BalancedUrlFrontier(int totalWorkers)
            : base("URL-FRONTIER")
        {
            locker = new object();
            totalWorkerThreads = totalWorkers;
            dnsCache = new DnsCache();

            queues = new PriorityQueue[totalWorkerThreads];
            for(int i = 0; i< totalWorkerThreads; i++)
            {
                queues[i] = new PriorityQueue();
            }
        }

        private int queueForUrl(GemiUrl url)
        {
            //we are trying to avoid adding URLs that are all served by the same
            //system from being dumped into different buckets, where we then overwhelm
            //that server. Basically Flounder, since all the subdomains are served by the same system

            //try and look up the ip address for this host. If we don't get one,
            //fall back to using the hostname.

            string address = dnsCache.GetLookup(url.Hostname);
            int hash = (address != null) ? address.GetHashCode() : url.Hostname.GetHashCode();

            return Math.Abs(hash) % totalWorkerThreads;
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
            var i = 0;
            foreach (string line in File.ReadAllLines(filename))
            {
                i++;
                GemiUrl url = null;
                try
                {
                    url = new GemiUrl(line);
                }
                catch (Exception)
                {
                    int x = 5;

                }
                if (url != null)
                {
                    Console.WriteLine($"{i}\t{url.NormalizedUrl}");
                    AddUrl(url);
                }
            }
        }
    }
}
