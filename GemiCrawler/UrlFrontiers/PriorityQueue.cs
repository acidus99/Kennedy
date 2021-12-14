using System;
using System.Collections.Generic;
using Gemi.Net;
using GemiCrawler.Utils;

namespace GemiCrawler.UrlFrontiers
{
    /// <summary>
    /// 
    /// </summary>
    internal class PriorityQueue
    {
        const int domainThreshold = 2000;

        object locker;

        /// <summary>
        /// our queue of URLs to crawl
        /// </summary>
        Queue<GemiUrl> highQueue;
        Queue<GemiUrl> lowQueue;

        /// <summary>
        /// tracks how many requests are going to a specific domain
        /// </summary>
        Bag<string> domainCounts;

        public PriorityQueue()
        {
            highQueue = new Queue<GemiUrl>();
            lowQueue = new Queue<GemiUrl>();
            domainCounts = new Bag<string>();

            locker = new object();
        }

        public void AddUrl(GemiUrl url)
        {
            if (IsHighPriority(url))
            {
                highQueue.Enqueue(url);
            } else
            {
                lowQueue.Enqueue(url);
            }
        }

        private bool IsHighPriority(GemiUrl url)
        {
            //have we accessed this domain to many times?
            if(domainCounts.Add(url.Authority) > domainThreshold)
            {
                return false;
            }
            var ext = url.FileExtension;
            //is this response likely to be geminitext, and thus have more links?
            //TODO support atom/xml at some point for gemlogs
            if(ext == "" || ext == "gmi")
            {
                return true;
            }
            //something else, defer
            return false;
        }

        public override string ToString()
            => $"High: {highQueue.Count} Low: {lowQueue.Count}"; 
        

        public int GetCount()
            => highQueue.Count + lowQueue.Count;

        public GemiUrl GetUrl()
        {
            GemiUrl ret = null;

            lock (locker)
            {
                ret = (highQueue.Count > 0) ?
                        highQueue.Dequeue() :
                        (lowQueue.Count > 0) ?
                            lowQueue.Dequeue() : null;
            }
            return ret;
        }
    }
}
