using System;
using System.Security.Cryptography;

using Kennedy.Data;

namespace Kennedy.CrawlData
{
    /// <summary>
    /// Document store implemented using an Object Store, backed onto a disk
    /// </summary>
    public class DocumentStore
    {
        ObjectStore store;

        public DocumentStore(string outputDir)
        {
            store = new ObjectStore(outputDir);
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
                var key = Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(resp.RequestUrl.ID))).ToLower();
                if(!store.StoreObject(key, resp.BodyBytes))
                {
                    throw new ApplicationException("Failed to store resp!");
                }
                return true;
            }
            return false;
        }

        private ulong GetLegacyID(long urlID)
        {
            //hack, we used to use ulong here. continue that here so we can read old page-store directories
            return unchecked((ulong)urlID);
        }

        public byte [] GetDocument(long urlID)
        {
            ulong id = GetLegacyID(urlID);
            var key = Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(id))).ToLower();
            return store.GetObject(key);            
        }
    }
}
