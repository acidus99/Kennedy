using System;
using Gemini.Net;

using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Filters
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

        public BlockResult IsUrlAllowed(UrlFrontierEntry entry)
        {
            int hits = DomainHits.Add(entry.Url.Authority);
            if (hits <= MaxHits)
            {
                return BlockResult.Allowed;
            }
            else
            {
                return new BlockResult(false, $"Domain hits exceeded. Hits = {hits}");
            }
        }
    }
}

