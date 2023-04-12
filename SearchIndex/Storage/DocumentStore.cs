using System;
using System.Security.Cryptography;

using Kennedy.Data;
using Gemini.Net;

namespace Kennedy.SearchIndex.Storage
{
    /// <summary>
    /// DocumentStore stores the contents of a URL, and retrieves it
    /// Document store implemented using an Object Store, backed onto a disk
    /// It it use
    /// </summary>
    public class DocumentStore : IDocumentStore
    {
        ObjectStore store;

        public DocumentStore(string outputDir)
        {
            store = new ObjectStore(outputDir);
        }

        public byte[] GetDocument(long urlID)
        {
            var key = GeyKey(urlID);
            return store.GetObject(key);
        }

        public bool RemoveDocument(long urlID)
        {
            var key = GeyKey(urlID);
            return store.RemoveObject(key);
        }

        /// <summary>
        /// returns if we successfully stored this response
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        public bool StoreDocument(ParsedResponse resp)
        {
            if (resp.IsSuccess & resp.HasBody)
            {
                var key = GeyKey(resp.RequestUrl.ID);
                if(!store.StoreObject(key, resp.BodyBytes))
                {
                    throw new ApplicationException("Failed to store resp!");
                }
                return true;
            }
            return false;
        }

        private string GeyKey(long urlID)
        {
            //hack, we used to use ulong here. continue that here so we can read old page-store directories
            ulong legacyUrlID = unchecked((ulong)urlID);
            return Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(legacyUrlID))).ToLower();
        }
    }
}
