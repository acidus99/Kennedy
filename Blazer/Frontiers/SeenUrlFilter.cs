﻿using System;
using System.Collections.Generic;

using Gemini.Net;
using Kennedy.Blazer.Utils;

namespace Kennedy.Blazer.Frontiers
{
    public class SeenUrlFilter : IUrlFilter
    { 
        /// <summary>
        /// Lookup table of URLs we have seen before
        /// </summary>
        Dictionary<ulong, bool> SeenUrls;

        object locker;
        ThreadSafeCounter seenCounter = new ThreadSafeCounter();

        public SeenUrlFilter()
        {
            locker = new object();
            SeenUrls = new Dictionary<ulong, bool>();
        }

        /// <summary>
        /// checks if we have not seen a URL before
        /// </summary>
        /// <param name="url"></param>
        /// <returns>URL has not been seen before during this crawl</returns>
        public bool IsUrlAllowed(GeminiUrl url)
        {
            lock (locker)
            {
                if (!SeenUrls.ContainsKey(url.DocID))
                {
                    SeenUrls[url.DocID] = true;
                    return true;
                }
            }
            seenCounter.Increment();
            return false;
        }
    }
}
