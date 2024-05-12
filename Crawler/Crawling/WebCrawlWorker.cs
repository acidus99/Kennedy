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
    const int MaxRetries = 5;

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
        var politeTracker = new PolitenessTracker();

        bool shouldRetryUrl = false;

        UrlFrontierEntry? entry = null;

        do
        {
            if (!shouldRetryUrl)
            {
                entry = Crawler.GetUrl(CrawlerID);
            }

            if (entry == null)
            {
                //if there was no work for this worker, sleep for 10 seconds
                Thread.Sleep(10000);
                continue;
            }

            GeminiResponse? response = null;

            if (!hostTracker.ShouldSendRequest(entry.Url))
            {
                Crawler.LogUrlRejection(entry.Url, "Host Health check failed");
                //if we got a URL entry, even if it was rejected for robots or health reasons,
                //we always have to call process on it, so th inflight/being processed counts work out
                Crawler.ProcessRequestResponse(entry, response);
                continue;
            }

            shouldRetryUrl = false;
            response = requestor.Request(entry);

            if (response == null)
            {
                Crawler.LogUrlRejection(entry.Url, "Excluded by Robots.txt");
                //if we got a URL entry, even if it was rejected for robots or health reasons,
                //we always have to call process on it, so th inflight/being processed counts work out
                Crawler.ProcessRequestResponse(entry, response);
                continue;
            }

            if (response.IsSlowDown)
            {
                politeTracker.IncreasePoliteness(entry.Url);

                if (entry.RetryCount < MaxRetries)
                {
                    shouldRetryUrl = true;
                    entry.RetryCount++;
                }
            }

            if (!shouldRetryUrl)
            {
                //if we aren't retrying it, add it to our host tracker
                hostTracker.AddResponse(response);
                //and process it
                Crawler.ProcessRequestResponse(entry, response);
            }

            Thread.Sleep(politeTracker.GetDelay(entry.Url));

        } while (Crawler.KeepWorkersAlive);
    }
}
