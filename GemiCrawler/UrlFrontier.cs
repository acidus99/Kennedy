using System;
using System.Collections.Generic;
using Gemi.Net;

namespace GemiCrawler
{
    /// <summary>
    /// Manages our queue of URLs to crawl. URL that have already been added are ignored
    /// </summary>
    public class CrawlQueue
    {
        object locker;

        /// <summary>
        /// our queue of URLs to crawl
        /// </summary>
        Queue<GemiUrl> queue;

        /// <summary>
        /// Lookup table of URLs we have seen before
        /// </summary>
        Dictionary<string, bool> SeenUrls;


        public CrawlQueue()
        {
            queue = new Queue<GemiUrl>();
            SeenUrls = new Dictionary<string, bool>();
            locker = new object();
        }

        public void EnqueueUrls(List<GemiUrl> urls)
        {
            int count = 0;
            foreach(var url in urls)
            {
                if(EnqueueUrl(url))
                {
                    count++;
                }
            }
            if (count > 0)
            {
                //Console.WriteLine($"\tAdded {count} URLs. Queue Length: {Count}");
            }
        }

        /// <summary>
        /// Adds a URL to the queue only if we haven't see it before
        /// </summary>
        /// <param name="url"></param>
        public bool EnqueueUrl(GemiUrl url)
        {
            lock(locker)
            {
                string normalizedUrl = url.NormalizedUrl;
                if(!SeenUrls.ContainsKey(normalizedUrl))
                {
                    //Console.WriteLine($"\tAdding new URL '{normalizedUrl}'");
                    SeenUrls.Add(normalizedUrl, true);
                    queue.Enqueue(url);
                    return true;
                }
            }
            return false;
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
