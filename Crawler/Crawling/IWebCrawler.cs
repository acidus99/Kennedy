using System;
using Gemini.Net;
namespace Kennedy.Crawler.Crawling;

public interface IWebCrawler
{
    bool KeepWorkersAlive { get; }

    GeminiUrl GetUrl(int crawlerID = 0);

    void AddRequested();

    void ProcessRequestResponse(GeminiResponse resp, Exception ex);
}
