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

        public BlockResult IsUrlAllowed(UrlFrontierEntry entry)
        {
            if(entry.DepthFromSeed < DepthLimit)
            {
                return BlockResult.Allowed;
            }
            return new BlockResult(false, $"Depth {entry.DepthFromSeed} exceeds depth limit of {DepthLimit}");
        }
    }
}

