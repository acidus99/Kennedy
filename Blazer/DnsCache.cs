using System;
using System.Net;

using Gemini.Net;
using System.Collections.Generic;

namespace Kennedy.Blazer
{
    public class DnsCache
    {
        public static readonly DnsCache Global = new DnsCache();

        object locker;
        Dictionary<string, IPAddress> cache;
        DnsWrapper client;

        public DnsCache()
        {
            locker = new object();
            cache = new Dictionary<string, IPAddress>();
            client = new DnsWrapper();
        }

        public IPAddress GetLookup(string hostname)
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
