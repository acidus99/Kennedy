using System;

using Gemini.Net;
using Kennedy.Blazer.Dns;
using Kennedy.Blazer.RobotsTxt;

namespace Kennedy.Blazer.Protocols
{
    public class GeminiProtocolHandler : Gemini.Net.GeminiRequestor
    {

        public new GeminiResponse Request(GeminiUrl url)
        {
            //use the DnsCache
            var ipAddress = DnsCache.Global.GetLookup(url.Hostname);

            if(ipAddress == null)
            {
                //could not resolve
                return new GeminiResponse(url)
                {
                    ConnectStatus = ConnectStatus.Error,
                    Meta = "Could not resolve hostname"
                };
            }

            //check if robots allows it
            if (RobotsChecker.Global.IsAllowed(url))
            {
                return base.Request(url, ipAddress);
            }
            //not allowed
            return null;
        }
    }
}

