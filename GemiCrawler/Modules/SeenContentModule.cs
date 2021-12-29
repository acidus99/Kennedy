using System;
using System.Collections.Generic;
using Gemi.Net;
using GemiCrawler.Utils;

namespace GemiCrawler.Modules
{
    public class SeenContentModule : AbstractModule
    {

        Dictionary<uint, bool> seenHashes;
        object locker;

        ThreadSafeCounter duplicateCounter;

        public SeenContentModule()
            : base("SEEN-CONTENT")
        {
            seenHashes = new Dictionary<uint, bool>();
            locker = new object();
            duplicateCounter = new ThreadSafeCounter();
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
                processedCounter.Increment();
                uint hash = IDGenerator.GetBodyHash(resp);
                lock (locker)
                {
                    if (!seenHashes.ContainsKey(hash))
                    {
                        seenHashes[hash] = true;
                        return false;
                    } else
                    {
                        duplicateCounter.Increment();
                    }
                }
            } else
            {
                // we want to process all error message, redirect, etc. So if it has no body, we haven't seen it
                return false;
            }
            return true;
        }

        protected override string GetStatusMesssage()
            => $"Responses w/ Bodies: {processedCounter.Count} Duplicates: {duplicateCounter.Count}";
    }
}
