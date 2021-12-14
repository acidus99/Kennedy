using System;
using System.Collections.Generic;
using Gemi.Net;

using GemiCrawler.Utils;
using System.IO;

namespace GemiCrawler.Modules
{
    public class SeenUrlModule : AbstractModule
    {
        /// <summary>
        /// Lookup table of URLs we have seen before
        /// TODO: track hashes instead of the full URL
        /// </summary>
        Dictionary<string, bool> SeenUrls;

        object locker;
        ThreadSafeCounter seenCounter = new ThreadSafeCounter();

        public SeenUrlModule()
            : base("SEENURL")
        {
            locker = new object();
            SeenUrls = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Checks if we a URL has been seen before. If not, it also adds it to our list
        /// </summary>
        /// <param name="url"></param>
        /// <returns>the URL has been seen before<returns>
        public bool CheckAndRecord(GemiUrl url)
        {
            var normalizedUrl = url.NormalizedUrl;
            processedCounter.Increment();
            lock(locker)
            {
                if(!SeenUrls.ContainsKey(normalizedUrl))
                {
                    SeenUrls[normalizedUrl] = true;
                    return false;
                }
            }
            seenCounter.Increment();
            return true;
        }

        protected override string GetStatusMesssage()
            => $"Urls seen: {processedCounter.Count} Seen Before: {seenCounter.Count}";
    }
}
