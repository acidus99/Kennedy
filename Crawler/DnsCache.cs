using System;
using System.Net;

using Gemini.Net;
using System.Collections.Generic;

namespace Kennedy.Crawler
{
    public class DnsCache
    {

        object locker;
        Dictionary<string, string> cache;

        public DnsCache()
        {
            locker = new object();
            cache = new Dictionary<string, string>();
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
                var result = Dns.GetHostEntry(hostname);
                if (result.AddressList.Length > 0)
                {
                    var address = result.AddressList[0].ToString();
                    lock (locker)
                    {
                        cache[hostname] = address;
                        return address;
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
