using System;
using System.Collections.Generic;
using HashDepot;
using Gemi.Net;
using GemiCrawler.Utils;

namespace GemiCrawler.Modules
{
    public class DomainLimiterModule : AbstractModule
    {
        public int MaxUrlsPerDomain { get; set; } = 15000;

        Bag<string> domainHits;
        object locker;

        ThreadSafeCounter discardCounter;

        public DomainLimiterModule()
            : base("Domain-Limiter")
        {
            domainHits = new Bag<string>();
            discardCounter = new ThreadSafeCounter();
        }

        /// <summary>
        /// checks if should add another URL for this domain to our URL Frontier or discard it
        /// </summary>
        /// <param name="resp"></param>
        /// <returns>if we have seen this resp body before</returns>
        public bool CheckAndRecord(GemiResponse resp)
        {

            processedCounter.Increment();
            int hits = domainHits.Add(resp.RequestUrl.Authority);
            if(hits > MaxUrlsPerDomain)
            {
                discardCounter.Increment();
                return false;
            }
            return true;
        }

        protected override string GetStatusMesssage()
            => $"Urls Checked: {processedCounter.Count}\tDiscarded: {discardCounter.Count}";
    }
}
