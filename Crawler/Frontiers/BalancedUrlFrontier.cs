using Gemini.Net;
using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Frontiers;

/// <summary>
/// Manages our queue of URLs to crawl
/// </summary>
public class BalancedUrlFrontier : IUrlFrontier
{
    object locker;

    /// <summary>
    /// our queue of URLs to crawl
    /// </summary>
    UrlQueue[] queues;

    int totalWorkerThreads;
    ThreadSafeCounter totalUrls;

    public int Count => GetCount();

    public int Total => totalUrls.Count;

    public BalancedUrlFrontier(int totalWorkers)
    {
        locker = new object();
        totalWorkerThreads = totalWorkers;
        totalUrls = new ThreadSafeCounter();

        queues = new UrlQueue[totalWorkerThreads];
        for (int i = 0; i < totalWorkerThreads; i++)
        {
            queues[i] = new UrlQueue();
        }
    }

    private int queueForUrl(GeminiUrl url)
    {
        //we are trying to avoid adding URLs that are all served by the same
        //system from being dumped into different buckets, where we then overwhelm
        //that server. Basically Flounder, since all the subdomains are served by the same system

        //try and look up the ip address for this host. If we don't get one,
        //fall back to using the hostname.

        string address = url.Authority;
        //string address = DnsCache.Global.GetLookup(url.Hostname);
        int hash = (address != null) ? address.GetHashCode() : url.Hostname.GetHashCode();

        return Math.Abs(hash) % totalWorkerThreads;
    }

    public void AddSeed(GeminiUrl url)
        => AddUrl(new UrlFrontierEntry
        {
            Url = url,
            DepthFromSeed = 0
        });

    private int GetCount()
    {
        int totalCount = 0;
        for (int i = 0; i < totalWorkerThreads; i++)
        {
            totalCount += queues[i].Count;
        }
        return totalCount;
    }

    public UrlFrontierEntry? GetUrl(int crawlerID)
        => queues[crawlerID].GetUrl();

    public void AddUrl(UrlFrontierEntry entry)
    {
        totalUrls.Increment();
        int queueID = queueForUrl(entry.Url);
        queues[queueID].AddUrl(entry);
    }
}
