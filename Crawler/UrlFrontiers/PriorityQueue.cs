using System;
using System.Linq;
using System.Collections.Generic;
using Gemini.Net;
using Kennedy.Crawler.Utils;

namespace Kennedy.Crawler.UrlFrontiers
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
        Queue<GeminiUrl> highQueue;
        Queue<GeminiUrl> lowQueue;

        /// <summary>
        /// tracks how many requests are going to a specific domain
        /// </summary>
        Bag<string> domainCounts;

        public PriorityQueue()
        {
            highQueue = new Queue<GeminiUrl>();
            lowQueue = new Queue<GeminiUrl>();
            domainCounts = new Bag<string>();

            locker = new object();
        }

        public void AddUrl(GeminiUrl url)
        {
            if (IsHighPriority(url))
            {
                lock (locker)
                {
                    highQueue.Enqueue(url);
                }
            } else
            {
                lock (locker)
                {
                    lowQueue.Enqueue(url);
                }
            }
        }

        private bool IsHighPriority(GeminiUrl url)
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

        public GeminiUrl GetUrl()
        {
            GeminiUrl ret = null;

            lock (locker)
            {
                ret = (highQueue.Count > 0) ?
                        highQueue.Dequeue() :
                        (lowQueue.Count > 0) ?
                            lowQueue.Dequeue() : null;
            }
            return ret;
        }

        public List<GeminiUrl> GetSnapshot()
        {
            var ret = new List<GeminiUrl>();
            ret.AddRange(highQueue);
            ret.AddRange(lowQueue);
            return ret;
        }

    }
}
