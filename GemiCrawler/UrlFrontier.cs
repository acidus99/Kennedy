using System;
using System.Collections.Generic;
using Gemi.Net;

namespace GemiCrawler
{
    /// <summary>
    /// Manages our queue of URLs to crawl. URL that have already been added are ignored
    /// </summary>
    public class UrlFrontier
    {
        object locker;

        /// <summary>
        /// our queue of URLs to crawl
        /// </summary>
        Queue<GemiUrl> queue;

        public UrlFrontier()
        {
            queue = new Queue<GemiUrl>();
            locker = new object();
        }

        /// <summary>
        /// Adds a URL to the queue only if we haven't see it before
        /// </summary>
        /// <param name="url"></param>
        public void EnqueueUrl(GemiUrl url)
        {
            queue.Enqueue(url);
        }

        public GemiUrl DequeueUrl()
        {
            GemiUrl ret = null;

            lock (locker)
            {
                ret = (queue.Count > 0) ? queue.Dequeue() : null;
            }
            return ret;
        }

        public int Count
            => queue.Count;

    }
}
