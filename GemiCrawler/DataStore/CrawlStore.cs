using System;
using Gemi.Net;

namespace GemiCrawler.DataStore
{
    public class CrawlStore : IDataStore
    {
        public CrawlStore()
        {
        }

        public void OutputStatus(string outputFile)
        {
            throw new NotImplementedException();
        }

        public bool Store(GemiUrl url, GemiResponse resp)
        {
            throw new NotImplementedException();
        }
    }
}
