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
        IUrlFilter SeenUrlFilter;

        public ThreadSafeCounter TotalUrls;
        public ThreadSafeCounter PassedUrls;

        public UrlFrontierWrapper(IUrlFrontier frontier)
        {
            UrlFrontier = frontier;
            SeenUrlFilter = new SeenUrlFilter();

            UrlFilters = new List<IUrlFilter>
            {
                new DepthFilter(),
                new DenyListFilter(),
                new DomainLimitFilter(),
            };

            TotalUrls = new ThreadSafeCounter();
            PassedUrls = new ThreadSafeCounter();
        }

        public string ModuleName => "Url Filters";

        private void AddUrl(UrlFrontierEntry entry)
        {
            TotalUrls.Increment();
            foreach (var filter in UrlFilters)
            {
                if (!filter.IsUrlAllowed(entry))
                {
                    return;
                }
            }

            //if everything else passed, then check the SeenUrlFilter
            //since that also makes note of the URL's hash
            if(SeenUrlFilter.IsUrlAllowed(entry))
            {
                //all allowed, pass it to the UrlFrontier
                PassedUrls.Increment();
                UrlFrontier.AddUrl(entry);
            }
        }

        public void AddUrls(int parentDepth, IEnumerable<FoundLink>? links, bool isRobotsLimits = true)
        {
            if (links != null)
            {
                foreach (var link in links)
                {
                    AddUrl(new UrlFrontierEntry
                    {
                        Url = link.Url,
                        IsRobotsLimited = isRobotsLimits,
                        DepthFromSeed = parentDepth + 1
                    });
                }
            }
        }

        public string GetStatus()
            => $"Url Filters\tInput: {TotalUrls.Count}\tPassed: {PassedUrls.Count}\tRejected: {TotalUrls.Count - PassedUrls.Count}";
    }
}