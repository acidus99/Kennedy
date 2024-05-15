using Gemini.Net;
using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Filters;

public class SeenUrlFilter : IUrlFilter
{
    static readonly BlockResult Denied = new BlockResult(false, "Already Seen URL");

    /// <summary>
    /// Lookup table of URLs we have seen before
    /// </summary>
    Dictionary<long, bool> SeenUrls;

    object locker;
    ThreadSafeCounter seenCounter = new ThreadSafeCounter();

    public SeenUrlFilter()
    {
        locker = new object();
        SeenUrls = new Dictionary<long, bool>();
    }

    /// <summary>
    /// Lets you peek if a URL has been seen before, without adding it to our list
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public bool IsAlreadySeen(GeminiUrl url)
        => SeenUrls.ContainsKey(url.ID);

    /// <summary>
    /// checks if we have not seen a URL before
    /// </summary>
    /// <param name="url"></param>
    /// <returns>URL has not been seen before during this crawl</returns>
    public BlockResult IsUrlAllowed(UrlFrontierEntry entry)
    {
        lock (locker)
        {
            if (!SeenUrls.ContainsKey(entry.Url.ID))
            {
                SeenUrls[entry.Url.ID] = true;
                return BlockResult.Allowed;
            }
        }
        seenCounter.Increment();
        return Denied;
    }

    /// <summary>
    /// Explicitly mark a URL as seen. Used wheen adding seeds to the frontier, since those URLs
    /// don't go through the typical code paths
    /// </summary>
    /// <param name="url"></param>
    public void MarkAsSeen(GeminiUrl url)
    {
        SeenUrls[url.ID] = true;
    }
}