using System;
using System.Collections.Generic;
using Gemi.Net;
using GemiCrawler.Modules;
using System.IO;

namespace GemiCrawler.UrlFrontiers
{
    /// <summary>
    /// Manages our queue of URLs to crawl
    /// </summary>
    public class BalancedUrlFrontier : AbstractModule, IUrlFrontier
    {
        object locker;

        /// <summary>
        /// our queue of URLs to crawl
        /// </summary>
        Queue<GemiUrl> [] queues;

        int totalWorkerThreads;

        public BalancedUrlFrontier(int totalWorkers)
            : base("URL-FRONTIER")
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

        public override void OutputStatus(string outputFile)
        {
            File.AppendAllText(outputFile, CreateLogLine($"Total Queue Size: {GetCount()}\n"));
        }

        public void SaveSnapshot(string outputFile)
        {
            var fout = new StreamWriter(outputFile, false, System.Text.Encoding.UTF8);
            lock (locker)
            {
                for (int i = 0; i < totalWorkerThreads; i++)
                {
                    fout.WriteLine($"============ Queue:{i} Size:{queues[i].Count}");
                    foreach (GemiUrl url in queues[i])
                    {
                        fout.WriteLine(url);
                    }
                }
                fout.Close();
            }
        }
    }
}
