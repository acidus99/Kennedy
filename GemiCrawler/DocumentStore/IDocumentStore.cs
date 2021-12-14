using Gemi.Net;

namespace GemiCrawler.DocumentStore
{
    /// <summary>
    /// Generic interface for storing crawl data
    /// </summary>
    public interface IDocumentStore
    {
        /// <summary>
        /// Returns the key used to store the resp
        /// </summary>
        string StoreDocument(GemiUrl url, GemiResponse resp);
    }
}
