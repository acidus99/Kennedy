using Gemi.Net;

namespace GemiCrawler.DocumentStore
{
    /// <summary>
    /// Generic interface for storing crawl data
    /// </summary>
    public interface IDocumentStore
    {
        /// <summary>
        /// stores the document in the respository
        /// </summary>
        void StoreDocument(GemiUrl url, GemiResponse resp);
    }
}
