using System;
using System.Collections.Generic;
using Gemi.Net;
using HashDepot;

namespace GemiCrawler.UrlFrontiers
{
    /// <summary>
    /// Manages our queue of URLs to crawl
    /// </summary>
    public class BalancedUrlFrontier : IUrlFrontier
    {
        object locker;

        /// <summary>
        /// our queue of URLs to crawl
        /// </summary>
        Queue<GemiUrl> [] queues;

        int totalWorkerThreads;

        public BalancedUrlFrontier(int totalWorkers)
        {
            locker = new object();
            totalWorkerThreads = totalWorkers;

            queues = new Queue<GemiUrl>[totalWorkerThreads];
            for(int i = 0; i< totalWorkerThreads; i++)
            {
                queues[i] = new Queue<GemiUrl>();
            }
        }

        private int queueForUrl(GemiUrl url)
        {
            return Math.Abs(url.Authority.GetHashCode()) % totalWorkerThreads;
        }
      
        public void AddUrl(GemiUrl url)
        {
            int queueID = queueForUrl(url);
            queues[queueID].Enqueue(url);
        }

        public int GetCount()
        {
            int totalCount = 0;
            for(int i=0;i<totalWorkerThreads; i++)
            {
                totalCount += queues[i].Count;
            }
            return totalCount;
        }   

        public GemiUrl GetUrl(int crawlerID = 0)
        {
            GemiUrl ret = null;

            lock (locker)
            {
                ret = (queues[crawlerID].Count > 0) ? queues[crawlerID].Dequeue() : null;
            }
            return ret;
        }
    }
}
