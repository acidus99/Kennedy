using System;

using Gemi.Net;

namespace GemiCrawler.DataStore
{
    /// <summary>
    /// Generic interface for storing crawl data
    /// </summary>
    public interface IDataStore
    {
        bool Store(GemiUrl url, GemiResponse resp);

        void OutputStatus(string outputFile);
    }
}
