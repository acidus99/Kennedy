﻿using System;
using Gemini.Net;
using System.Security.Cryptography;

namespace Gemini.Net.Crawler.DocumentStore
{
    /// <summary>
    /// Document store implemented using an Object Store, backed onto a disk
    /// </summary>
    public class DocStore
    {
        ObjectStore store;

        public DocStore(string outputDir)
        {
            store = new ObjectStore(outputDir);
        }

        /// <summary>
        /// returns if we successfully stored this response
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        public bool StoreDocument(GemiResponse resp)
        {
            if (resp.IsSuccess & resp.HasBody)
            {
                var key = Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(resp.RequestUrl.DocID))).ToLower();
                if(!store.StoreObject(key, resp.BodyBytes))
                {
                    throw new ApplicationException("Failed to store resp!");
                }
                return true;
            }
            return false;
        }

        public byte [] GetDocument(ulong docID)
        {
            var key = Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(docID))).ToLower();
            return store.GetObject(key);            
        }
    }
}