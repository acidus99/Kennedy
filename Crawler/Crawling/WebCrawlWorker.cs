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
        GeminiUrl url = null;

        GeminiProtocolHandler requestor = new GeminiProtocolHandler();

        do
        {
            url = Crawler.GetUrl(CrawlerID);

            if (url != null)
            {
                if (hostTracker.ShouldSendRequest(url))
                {
                    var resp = requestor.Request(url);

                    hostTracker.AddResponse(resp);
                    Crawler.ProcessRequestResponse(resp, requestor.LastException);
                    if (resp.ConnectStatus != ConnectStatus.Skipped)
                    {
                        Thread.Sleep(delayMs);
                    }
                }
            
            }
            else
            {
                Thread.Sleep(10000);
            }
        } while (Crawler.KeepWorkersAlive);
    }
}
