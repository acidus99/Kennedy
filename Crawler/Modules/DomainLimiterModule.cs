using System;
using System.Collections.Generic;

using Gemini.Net;
using Kennedy.Crawler.Utils;
using Kennedy.Data.Utils;

namespace Kennedy.Crawler.Modules
{
    public class DomainLimiterModule : AbstractUrlModule
    {
        public int MaxUrlsPerDomain { get; set; } = 35000;

        Bag<string> domainHits;
        object locker;

        ThreadSafeCounter discardCounter;

        public DomainLimiterModule()
            : base("Domain-Limiter")
        {
            domainHits = new Bag<string>();
            discardCounter = new ThreadSafeCounter();
        }

        protected override string GetStatusMesssage()
            => $"Urls Checked: {processedCounter.Count}\tDiscarded: {discardCounter.Count}";

        /// <summary>
        /// checks if a URL has not yet exceeded any limits for a specific domain/authority
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public override bool IsUrlAllowed(GeminiUrl url)
        {
            processedCounter.Increment();
            int hits = domainHits.Add(url.Authority);
            if (hits > MaxUrlsPerDomain)
            {
                discardCounter.Increment();
                return false;
            }
            return true;
        }
    }
}
