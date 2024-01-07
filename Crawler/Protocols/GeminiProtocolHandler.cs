using System;

using Gemini.Net;
using Kennedy.Crawler.Dns;
using Kennedy.Data;

namespace Kennedy.Crawler.Protocols
{
    public class GeminiProtocolHandler
    {
        GeminiRequestor requestor = new GeminiRequestor
        {
            MaxResponseSize = 10 * 1024 * 1024
        };

        public GeminiResponse? Request(UrlFrontierEntry entry)
        {
            //use the DnsCache
            var ipAddress = DnsCache.Global.GetLookup(entry.Url.Hostname);

            if(ipAddress == null)
            {
                //could not resolve
                return new GeminiResponse(entry.Url)
                {
                    StatusCode = GeminiParser.ConnectionErrorStatusCode,
                    Meta = "Could not resolve hostname"
                };
            }

            if (entry.IsRobotsLimited)
            {
                if (RobotsChecker.Global.IsAllowed(entry.Url))
                {
                    return requestor.Request(entry.Url, ipAddress);
                }
                return null;
            }
            else
            {
                return requestor.Request(entry.Url, ipAddress);
            }
        }
    }
}

