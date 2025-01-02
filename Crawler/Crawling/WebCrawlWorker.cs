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

    private ConnectivityTracker ConnectivityTracker;

    // The constructor obtains the state information.
    public WebCrawlWorker(IWebCrawler crawler, int id)
    {
        Crawler = crawler;
        CrawlerID = id;
        ConnectivityTracker = new ConnectivityTracker();
    }

    public void DoWork()
    {
        var requestor = new GeminiProtocolHandler();
        var politeTracker = new PolitenessTracker();

        bool shouldRetryUrl = false;

        UrlFrontierEntry? entry = null;

        do
        {
            if (!shouldRetryUrl)
            {
                entry = GetNextUrl();
            }

            if (entry == null)
            {
                //if there was no work for this worker, sleep for 10 seconds
                Thread.Sleep(10000);
                continue;
            }

            GeminiResponse? response;

            ConnectivityInfo connectivity = ConnectivityTracker.GetConnectivityInfo(entry.Url);

            //do we have permanent connectivity issues?
            if(connectivity.HasTerminalIssue)
            {
                response = new GeminiResponse(entry.Url)
                {
                    StatusCode = GeminiParser.ConnectionErrorStatusCode,
                    Meta = connectivity.ErrorMessage
                };
                //if we got a URL entry, even if it was discarded for connectivity issues,
                //we always have to call process on it, so th inflight/being processed counts work out
                Crawler.ProcessRequestResponse(entry, response);
                continue;
            }

            //do we have temporary connectivity issues?
            if(connectivity.HasTemporaryIssues())
            {
                Crawler.LogRejectedUrl(entry.Url, "Connectivity check failed");
                //if we got a URL entry, even if it was connectivity issues
                //we always have to call process on it, so th inflight/being processed counts work out
                Crawler.ProcessSkippedRequest(entry, SkippedReason.SkippedForConnectivity);
                continue;
            }

            shouldRetryUrl = false;
            response = requestor.Request(entry);

            //rejected by robots?
            if (response == null)
            {
                Crawler.LogRejectedUrl(entry.Url, "Excluded by Robots.txt");
                //if we got a URL entry, even if it was rejected for robots reasons,
                //we always have to call process on it, so th inflight/being processed counts work out
                Crawler.ProcessSkippedRequest(entry, SkippedReason.SkippedForRobots);
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
                //if we aren't retrying it, record response for the connectivity info
                connectivity.RecordResponse(response);
                //and process it
                Crawler.ProcessRequestResponse(entry, response);
            }

            Thread.Sleep(politeTracker.GetDelay(entry.Url));

        } while (Crawler.KeepWorkersAlive);

        //now drain our queue, if necessary
        DrainRemainingQueue();
    }

    private UrlFrontierEntry? GetNextUrl()
        => Crawler.GetUrl(CrawlerID);

    private void DrainRemainingQueue()
    {
        UrlFrontierEntry? entry;
        while ((entry = GetNextUrl()) != null)
        {
            //if we have connectivity issues, don't add it to the remaining
            if(ConnectivityTracker.GetConnectivityInfo(entry.Url).HasTerminalIssue)
            {
                continue;
            }

            //if it would have been excluded by robots.txt, skip it
            if(!RobotsChecker.Global.IsAllowedOffline(entry.Url))
            {
                continue;
            }
            Crawler.LogRemainingUrl(entry);
        }
    }
}
