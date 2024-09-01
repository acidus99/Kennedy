using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Crawler.Crawling;

/// <summary>
/// Interface exposed to crawl threads
/// </summary>
public interface IWebCrawler
{
    bool KeepWorkersAlive { get; }

    UrlFrontierEntry? GetUrl(int crawlerID = 0);

    void LogRemainingUrl(UrlFrontierEntry entry);

    void LogRejectedUrl(GeminiUrl url, string rejectionType, string specificRule = "");

    void ProcessRequestResponse(UrlFrontierEntry entry, GeminiResponse response);

    void ProcessSkippedRequest(UrlFrontierEntry entry, SkippedReason reason);

    void ProcessRobotsResponse(GeminiResponse response);
}