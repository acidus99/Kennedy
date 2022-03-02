using System;
using System.Net;

using Gemini.Net;
using System.Collections.Generic;
using Kennedy.Crawler.Utils;

namespace Kennedy.Crawler
{
    public class DnsCache
    {

        object locker;
        Dictionary<string, string> cache;
        DnsWrapper client;

        public DnsCache()
        {
            locker = new object();
            cache = new Dictionary<string, string>();
            client = new DnsWrapper();
        }

        public string GetLookup(string hostname)
        {
            lock(locker)
            {
                if (cache.ContainsKey(hostname))
                {
                    return cache[hostname];
                }
            }
            //look it up
            try
            {
                var result = client.DoLookup(hostname);
                if (result != null)
                {
                    lock (locker)
                    {
                        cache[hostname] = result;
                        return result;
                    }
                }
            }
            catch (Exception)
            { }

            lock (locker)
            {
                cache[hostname] = null;
            }
            return null;
        }
    }
}
