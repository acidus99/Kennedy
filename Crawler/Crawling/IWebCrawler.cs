using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

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

    void LogUrlRejection(GeminiUrl url, string rejectionType, string specificRule = "");

    void ProcessRequestResponse(UrlFrontierEntry entry, GeminiResponse? response);

    void ProcessRobotsResponse(GeminiResponse response);
}
