using Gemini.Net;
using Kennedy.Crawler.Filters;
using Kennedy.Crawler.Logging;
using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Frontiers;

public class UrlFrontierWrapper
{
    IUrlFrontier UrlFrontier;
    List<IUrlFilter> UrlFilters;
    SeenUrlFilter SeenUrlFilter;

    Dictionary<string, bool> SeedAuthorities = new Dictionary<string, bool>();
    public bool LimitCrawlToSeeds { get; set; } = false;

    RejectedUrlLogger UrlLogger;
    BlockListFilter BlockListFilter;

    public ThreadSafeCounter TotalUrls;
    public ThreadSafeCounter PassedUrls;

    public UrlFrontierWrapper(IUrlFrontier frontier, RejectedUrlLogger urlLogger)
    {
        UrlFrontier = frontier;
        UrlLogger = urlLogger;
        SeenUrlFilter = new SeenUrlFilter();
        BlockListFilter = new BlockListFilter();

        UrlFilters = new List<IUrlFilter>
        {
            new DepthFilter(),
            BlockListFilter,
            //no more domain limiter
            //new DomainLimitFilter(),
        };

        TotalUrls = new ThreadSafeCounter();
        PassedUrls = new ThreadSafeCounter();
    }

    public bool AddSeed(GeminiUrl seedUrl)
    {
        if (LimitCrawlToSeeds)
        {
            if (!SeedAuthorities.ContainsKey(seedUrl.Authority))
            {
                SeedAuthorities[seedUrl.Authority] = true;
            }
        }

        if (BlockListFilter.IsUrlAllowed(seedUrl).IsAllowed)
        {
            UrlFrontier.AddSeed(seedUrl);
            SeenUrlFilter.MarkAsSeen(seedUrl);
            return true;
        }
        return false;
    }

    private void AddUrl(UrlFrontierEntry entry)
    {
        BlockResult result;
        TotalUrls.Increment();
        //peek if we have seen it before. If we have, no need to check the other filters
        if (SeenUrlFilter.IsAlreadySeen(entry.Url))
        {
            return;
        }

        if (LimitCrawlToSeeds & !SeedAuthorities.ContainsKey(entry.Url.Authority))
        {
            UrlLogger.LogRejection(entry.Url, "Not a seed URL authority");
            return;
        }

        foreach (var filter in UrlFilters)
        {
            result = filter.IsUrlAllowed(entry);
            if (!result.IsAllowed)
            {
                UrlLogger.LogRejection(entry.Url, result.RejectionType, result.SpecificRule);
                return;
            }
        }

        //if everything else passed, then check the SeenUrlFilter
        //since that also makes note of the URL's hash
        result = SeenUrlFilter.IsUrlAllowed(entry);
        if (result.IsAllowed)
        {
            //all allowed, pass it to the UrlFrontier
            PassedUrls.Increment();
            UrlFrontier.AddUrl(entry);
        }
        //no need to log seen URLs, since that is a ton of links
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
                    DepthFromSeed = parentDepth + 1,
                    IsProactive = !isRobotsLimits
                });
            }
        }
    }
}