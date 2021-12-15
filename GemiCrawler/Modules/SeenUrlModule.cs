using System;
using System.Collections.Generic;
using Gemi.Net;

using GemiCrawler.Utils;

namespace GemiCrawler.Modules
{
    public class SeenUrlModule : AbstractUrlModule
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
        /// checks if we have not seen a URL before
        /// </summary>
        /// <param name="url"></param>
        /// <returns>URL has not been seen before during this crawl</returns>
        public override bool IsUrlAllowed(GemiUrl url)
        {
            var normalizedUrl = url.NormalizedUrl;
            processedCounter.Increment();
            lock (locker)
            {
                if (!SeenUrls.ContainsKey(normalizedUrl))
                {
                    SeenUrls[normalizedUrl] = true;
                    return true;
                }
            }
            seenCounter.Increment();
            return false;
        }

        protected override string GetStatusMesssage()
            => $"Urls seen: {processedCounter.Count} Seen Before: {seenCounter.Count}";
    }
}
