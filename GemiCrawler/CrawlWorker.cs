using System;
using System.Threading;
using Gemi.Net;
namespace GemiCrawler
{
    /// <summary>
    /// Thread that pulls jobs for a specific authoriy
    /// </summary>
    internal class CrawlWorker
    {

        /// <summary>
        /// how long should we wait between requests to the same authority
        /// </summary>
        const int delayMs = 1000;

        public ICrawler Crawler;
        public int CrawlerID;

        // The constructor obtains the state information.
        public CrawlWorker(ICrawler crawler, int id)
        {
            Crawler = crawler;
            CrawlerID = id;
        }

        public string Name
            => Thread.CurrentThread.Name;

        // The thread procedure performs the task, such as formatting
        // and printing a document.
        public void DoWork()
        {
            GemiUrl url = null;

            GemiRequestor requestor = new GemiRequestor();

            do
            {
                url =  Crawler.GetNextUrl(CrawlerID);
                if (url != null)
                {
                    //Console.WriteLine($"{Name} is fetching '{url}'");
                    var resp = requestor.Request(url);
                    Crawler.ProcessResult(url, resp, requestor.LastException);
                    //Console.WriteLine($"{Name} has processed '{url}'");
                }
                Thread.Sleep(delayMs);
            } while (Crawler.KeepWorkersAlive);

            Console.WriteLine($"{Name} terminating since KeepWorkersAlive is false");
        }
    }
}
