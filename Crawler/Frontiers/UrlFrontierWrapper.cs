using System;
using System.Linq;


using Gemini.Net;

using Kennedy.Data;

using Kennedy.Crawler.Frontiers;
using Kennedy.Crawler.Logging;
using Kennedy.Crawler.Utils;

namespace Kennedy.Crawler.Frontiers
{
    public class UrlFrontierWrapper : IStatusProvider
    {
        IUrlFrontier UrlFrontier;
        List<IUrlFilter> UrlFilters;

        public ThreadSafeCounter TotalUrls;
        public ThreadSafeCounter PassedUrls;

        public UrlFrontierWrapper(IUrlFrontier frontier)
        {
            UrlFrontier = frontier;
            UrlFilters = new List<IUrlFilter>
            {
                new SeenUrlFilter(),
                new DenyListFilter(),
                new DomainLimitFilter(),
            };

            TotalUrls = new ThreadSafeCounter();
            PassedUrls = new ThreadSafeCounter();
        }

        public string ModuleName => "Url Filters";

        public void AddUrl(GeminiUrl url)
        {
            TotalUrls.Increment();
            foreach (var filter in UrlFilters)
            {
                if (!filter.IsUrlAllowed(url))
                {
                    return;
                }
            }
            //all allowed, pass it to the UrlFrontier
            PassedUrls.Increment();
            UrlFrontier.AddUrl(url);
        }

        public void AddUrls(IEnumerable<GeminiUrl>? urls)
        {
            if (urls != null)
            {
                foreach (var url in urls)
                {
                    AddUrl(url);
                }
            }
        }

        public void AddUrls(IEnumerable<FoundLink>? links)
        {
            if (links != null)
            {
                foreach (var link in links)
                {
                    AddUrl(link.Url);
                }
            }
        }

        public string GetStatus()
            => $"Url Filters\tInput: {TotalUrls.Count}\tPassed: {PassedUrls.Count}\tRejected: {TotalUrls.Count - PassedUrls.Count}";
    }
}