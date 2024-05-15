using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Crawler.Frontiers;

public interface IUrlFrontier
{
    /// <summary>
    /// How many URLs are in the frontier
    /// </summary>
    int Count { get; }

    int Total { get; }

    void AddSeed(GeminiUrl url);

    /// <summary>
    /// Adds a URL to the frontier
    /// </summary>
    /// <param name="url"></param>
    void AddUrl(UrlFrontierEntry entry);

    /// <summary>
    /// Gets the next URL from the frontier
    /// </summary>
    /// <returns></returns>
    UrlFrontierEntry? GetUrl(int crawlerID);
}