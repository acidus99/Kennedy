using System;
using System.Collections.Generic;
using Gemi.Net;
using System.IO;

namespace GemiCrawler.UrlFrontiers
{
    /// <summary>
    /// Manages our queue of URLs to crawl. URL that have already been added are ignored
    /// </summary>
    public class BasicUrlFrontier :IUrlFrontier
    {
        object locker;

        /// <summary>
        /// our queue of URLs to crawl
        /// </summary>
        Queue<GemiUrl> queue;

        public BasicUrlFrontier()
        {
            queue = new Queue<GemiUrl>();
            locker = new object();
        }

        public void AddUrl(GemiUrl url)
        {
            queue.Enqueue(url);
        }

        public int GetCount()
            => queue.Count;

        public GemiUrl GetUrl(int crawlerID = 0)
        {
            GemiUrl ret = null;

            lock (locker)
            {
                ret = (queue.Count > 0) ? queue.Dequeue() : null;
            }
            return ret;
        }

        public void OutputStatus(string outputFile)
        {
            File.AppendAllText(outputFile, $"Total Queue Size: {GetCount()}\n");
        }

    }
}
