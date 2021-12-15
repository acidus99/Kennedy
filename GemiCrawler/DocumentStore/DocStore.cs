using System;
using Gemi.Net;
using System.Security.Cryptography;

namespace GemiCrawler.DocumentStore
{
    /// <summary>
    /// Document store implemented using an Object Store, backed onto a disk
    /// </summary>
    public class DocStore : IDocumentStore
    {
        ObjectStore store;

        public DocStore(string outputDir)
        {
            store = new ObjectStore(outputDir);
        }

        public string StoreDocument(GemiUrl url, GemiResponse resp)
        {
            var key = "";
            if (resp.IsSuccess & resp.HasBody)
            {
                key = Convert.ToHexString(MD5.HashData(resp.BodyBytes)).ToLower();
                if(!store.StoreObject(key, resp.BodyBytes))
                {
                    throw new ApplicationException("Failed to store resp!");
                }
            }
            return key;
        }
    }
}
