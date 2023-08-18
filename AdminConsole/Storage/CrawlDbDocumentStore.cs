using System;
using System.Security.Cryptography;

using Kennedy.Data;
using Gemini.Net;

namespace Kennedy.AdminConsole.Storage
{
    /// <summary>
    /// Get the contents of a URL based on the hash of the URL.
    /// Implemented using an Object Store, backed onto a disk
    ///
    /// This was used by the crawl-db format of crawls
    /// </summary>
    public class CrawlDbDocumentStore
    {
        ObjectStore store;

        public CrawlDbDocumentStore(string outputDir)
        {
            store = new ObjectStore(outputDir);
        }

        public byte[] GetDocument(long urlID)
        {
            var key = GeyKey(urlID);
            return store.GetObject(key);
        }

        private string GeyKey(long urlID)
        {
            //hack, we used to use ulong here. continue that here so we can read old page-store directories
            ulong legacyUrlID = unchecked((ulong)urlID);
            return Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(legacyUrlID))).ToLower();
        }
    }
}
