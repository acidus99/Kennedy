using System;
using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Crawler.Filters
{
	public class DepthFilter : IUrlFilter
	{
        const int DefaultLimit = 25;

        int DepthLimit;

		public DepthFilter(int limit = DefaultLimit)
		{
            DepthLimit = limit;
		}

        public UrlFilterResult IsUrlAllowed(UrlFrontierEntry entry)
        {
            if(entry.DepthFromSeed < DepthLimit)
            {
                return UrlFilterResult.Allowed;
            }
            return new UrlFilterResult(false, $"Depth {entry.DepthFromSeed} exceeds depth limit of {DepthLimit}");
        }
    }
}

