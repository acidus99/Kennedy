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
        /// </summary>
        Dictionary<ulong, bool> SeenUrls;

        object locker;
        ThreadSafeCounter seenCounter = new ThreadSafeCounter();

        public SeenUrlModule()
            : base("SEENURL")
        {
            locker = new object();
            SeenUrls = new Dictionary<ulong, bool>();
        }

        /// <summary>
        /// checks if we have not seen a URL before
        /// </summary>
        /// <param name="url"></param>
        /// <returns>URL has not been seen before during this crawl</returns>
        public override bool IsUrlAllowed(GemiUrl url)
        {
            processedCounter.Increment();
            lock (locker)
            {
                if (!SeenUrls.ContainsKey(url.DocID))
                {
                    SeenUrls[url.DocID] = true;
                    return true;
                }
            }
            seenCounter.Increment();
            return false;
        }

        public void PopulateWithSeenIDs(List<ulong> ids)
        {
            foreach (var id in ids)
            {
                SeenUrls[id] = true;
            }
        }


        protected override string GetStatusMesssage()
            => $"Urls seen: {processedCounter.Count} Seen Before: {seenCounter.Count}";
    }
}
