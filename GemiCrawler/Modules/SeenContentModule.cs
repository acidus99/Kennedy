using System;
using System.Collections.Generic;
using HashDepot;

using Gemi.Net;

namespace GemiCrawler.Modules
{
    public class SeenContentModule : AbstractModule
    {

        Dictionary<uint, bool> seenHashes;
        object locker;


        public SeenContentModule()
            : base("SEEN-CONTENT")
        {
            seenHashes = new Dictionary<uint, bool>();
            locker = new object();
        }

        /// <summary>
        /// checks if we have seen this response body before. records it if we have not
        /// </summary>
        /// <param name="resp"></param>
        /// <returns>if we have seen this resp body before</returns>
        public bool CheckAndRecord(GemiResponse resp)
        {
            //TODO could only do this for gemini texts if I really cared to
            if (resp.HasBody)
            {
                uint hash = XXHash.Hash32(resp.BodyBytes);
                lock (locker)
                {
                    if (!seenHashes.ContainsKey(hash))
                    {
                        seenHashes[hash] = true;
                        return false;
                    }
                }
            } else
            {
                // we want to process all error message, redirect, etc. So if it has no body, we haven't seen it
                return false;
            }
            return true;
        }
    }
}
