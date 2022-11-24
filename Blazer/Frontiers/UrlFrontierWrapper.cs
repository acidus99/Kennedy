using System;
using Gemini.Net;
using Kennedy.Blazer.Frontiers;

namespace Kennedy.Blazer.Frontiers
{
    public class UrlFrontierWrapper
    {
        IUrlFrontier UrlFrontier;

        List<IUrlFilter> UrlFilters;

        public UrlFrontierWrapper(IUrlFrontier frontier)
        {
            UrlFrontier = frontier;
            UrlFilters = new List<IUrlFilter>
            {
                new SeenUrlFilter(),
            };
        }

        public void AddUrl(GeminiUrl url)
        {
            foreach (var filter in UrlFilters)
            {
                if (!filter.IsUrlAllowed(url))
                {
                    return;
                }
            }
            //all allowed, pass it to the UrlFrontier
            UrlFrontier.AddUrl(url);
        }

        public void AddUrls(IEnumerable<GeminiUrl> urls)
        {
            foreach(var url in urls)
            {
                AddUrl(url);
            }
        }
    }
}
