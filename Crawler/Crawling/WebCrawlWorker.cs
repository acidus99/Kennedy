using System;
using System.Threading;

using Gemini.Net;
using Kennedy.Crawler.Protocols;
using Kennedy.Data;

namespace Kennedy.Crawler.Crawling;

/// <summary>
/// Thread that pulls jobs for a specific set of authorities
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

    public void DoWork()
    {
        var requestor = new GeminiProtocolHandler();
        var hostTracker = new HostHealthTracker();

        do
        {
            UrlFrontierEntry? entry = Crawler.GetUrl(CrawlerID);

            if (entry != null)
            {
                GeminiResponse? response = null;

                if (!hostTracker.ShouldSendRequest(entry.Url))
                {
                    Crawler.LogUrlRejection(entry.Url, "Host Health check failed");
                }
                else
                {
                    response = requestor.Request(entry);

                    if (response == null)
                    {
                        Crawler.LogUrlRejection(entry.Url, "Excluded by Robots.txt");
                    }

                    hostTracker.AddResponse(response);
                }

                //if we got a URL entry, even if it was rejected for robots or health reasons,
                //we always have to call process on it, so th inflight/being processed counts work out
                Crawler.ProcessRequestResponse(entry, response);

                //if we got a response, we need to wait before doing the next
                if (response != null)
                {
                    //TODO implement backoff timing here based on 44 responses
                    Thread.Sleep(delayMs);
                }
            }
            else
            {
                //if there was no work for this worker, sleep for 10 seconds
                Thread.Sleep(10000);
            }
        } while (Crawler.KeepWorkersAlive);
    }
}
