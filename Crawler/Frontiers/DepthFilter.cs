using System;
using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Crawler.Frontiers
{
	public class DepthFilter : IUrlFilter
	{
        const int DefaultLimit = 25;

        int DepthLimit;

		public DepthFilter(int limit = DefaultLimit)
		{
            DepthLimit = limit;
		}

        public bool IsUrlAllowed(UrlFrontierEntry entry)
            => entry.DepthFromSeed < DepthLimit;
    }
}

