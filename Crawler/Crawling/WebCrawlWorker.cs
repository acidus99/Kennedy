using System;
using System.Threading;

using Gemini.Net;
using Kennedy.Crawler.Protocols;

namespace Kennedy.Crawler.Crawling;

/// <summary>
/// Thread that pulls jobs for a specific authoriy
/// </summary>
internal class WebCrawlWorker
{
    /// <summary>
    /// how long should we wait between requests to the same authority
    /// </summary>
    const int delayMs = 500;

    public IWebCrawler Crawler;
    public int CrawlerID;

    // The constructor obtains the state information.
    public WebCrawlWorker(IWebCrawler crawler, int id)
    {
        Crawler = crawler;
        CrawlerID = id;
    }

    // The thread procedure performs the task, such as formatting
    // and printing a document.
    public void DoWork()
    {
        GeminiUrl url = null;

        GeminiProtocolHandler requestor = new GeminiProtocolHandler();

        do
        {
            url = Crawler.GetUrl(CrawlerID);
            if (url != null)
            {


                var resp = requestor.Request(url);
                Crawler.ProcessRequestResponse(resp, requestor.LastException);
                Thread.Sleep(delayMs);
            }
            else
            {
                Thread.Sleep(10000);
            }
        } while (Crawler.KeepWorkersAlive);
    }
}
