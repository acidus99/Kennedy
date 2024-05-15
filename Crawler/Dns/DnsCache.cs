using System.Net;

namespace Kennedy.Crawler.Dns;

public class DnsCache
{
    public static readonly DnsCache Global = new DnsCache();

    object locker;
    Dictionary<string, IPAddress?> cache;
    DnsWrapper client;

    public DnsCache()
    {
        locker = new object();
        cache = new Dictionary<string, IPAddress?>();
        client = new DnsWrapper();
    }

    /// <summary>
    /// Does a DNS lookup on a hostname. 
    /// </summary>
    /// <param name="hostname"></param>
    /// <returns>null if hosntame doesn't resolve</returns>
    public IPAddress? GetLookup(string hostname)
    {
        lock (locker)
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