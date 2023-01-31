using System;
using Gemini.Net;

using Kennedy.Crawler.Utils;

namespace Kennedy.Crawler.Frontiers
{
	public class DomainLimitFilter : IUrlFilter
	{
        int MaxHits;
        Bag<String> DomainHits;

		public DomainLimitFilter(int maxHits = 15000)
		{
            DomainHits = new Bag<string>();
            MaxHits = maxHits;
		}

        public bool IsUrlAllowed(GeminiUrl url)
        {
            int hits = DomainHits.Add(url.Authority);
            return (hits <= MaxHits);
        }
    }
}

