using System;
using System.Threading;

using Gemini.Net;
using Kennedy.Crawler.Protocols;
using Kennedy.Data;

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

    HostHealthTracker hostTracker;

    // The constructor obtains the state information.
    public WebCrawlWorker(IWebCrawler crawler, int id)
    {
        Crawler = crawler;
        CrawlerID = id;
        hostTracker = new HostHealthTracker();
    }

    // The thread procedure performs the task, such as formatting
    // and printing a document.
    public void DoWork()
    {
        UrlFrontierEntry? entry = null;

        GeminiProtocolHandler requestor = new GeminiProtocolHandler();

        do
        {
            entry = Crawler.GetUrl(CrawlerID);

            if (entry != null)
            {
                GeminiResponse? response = null;

                if (hostTracker.ShouldSendRequest(entry.Url))
                {
                    response = requestor.Request(entry);
                    hostTracker.AddResponse(response);
                }

                Crawler.ProcessRequestResponse(entry, response);
                //if we got a response, we need to wait before doing the next
                if (response != null)
                {
                    Thread.Sleep(delayMs);
                }
            }
            else
            {
                Thread.Sleep(10000);
            }
        } while (Crawler.KeepWorkersAlive);
    }
}
