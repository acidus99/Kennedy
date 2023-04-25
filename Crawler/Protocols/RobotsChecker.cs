using System;

using Gemini.Net;
using Kennedy.Crawler.Dns;
using Kennedy.Crawler.Crawling;
using Kennedy.Data.RobotsTxt;

namespace Kennedy.Crawler.Protocols
{

    /// <summary>
    /// returns if a URL is allowed or not, based on it's Robots.txt
    /// //grabs new Robots.txt files ondemand
    /// </summary>
    public class RobotsChecker
    {
        public static RobotsChecker Global = new RobotsChecker();

        Dictionary<string, RobotsTxtFile> Cache;

        public RobotsChecker()
        {
            Cache = new Dictionary<string, RobotsTxtFile>();
        }

        public IWebCrawler? Crawler { get; set; } = null;

        public bool IsAllowed(string url)
            => IsAllowed(new GeminiUrl(url));

        public bool IsAllowed(GeminiUrl url)
        {
            //check if its in our cache

            var key = GetCacheKey(url);

            RobotsTxtFile robots = null;

            if (!Cache.TryGetValue(key, out robots))
            {
                robots = LoadRobotsIntoCache(key, url.Hostname, url.Port);
            }

            if (robots != null)
            {
                return robots.IsPathAllowed("indexer", url.Path);
            }

            //nothing explicitly telling me no, so allow it
            return true;
        }

        public RobotsTxtFile GetFromCache(string domain, int port)
            => Cache.GetValueOrDefault(GetCacheKey(domain, port));

        private string GetCacheKey(GeminiUrl url)
            => url.Authority;

        private string GetCacheKey(string domain, int port)
            => $"{domain}:{port}";

        /// <summary>
        /// Downloads the Robots.txt file for a host, parses it, and adds it to the cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hostname"></param>
        /// <param name="port"></param>
        private RobotsTxtFile LoadRobotsIntoCache(string key, string hostname, int port)
        {
            try
            {
                var contents = FetchRobots(hostname, port);
                RobotsTxtFile robots = new RobotsTxtFile(contents);
                if (!robots.IsMalformed)
                {
                    Cache[key] = robots;
                    return robots;
                }
            }
            catch (Exception)
            {

            }
            Cache[key] = null;
            return null;
        }

        /// <summary>
        /// fetches Robots from the network
        /// </summary>
        /// <param name="hostname"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private string FetchRobots(string hostname, int port)
        {
            var robotsUrl = new GeminiUrl($"gemini://{hostname}:{port}/robots.txt?kennedy-crawler");

            Gemini.Net.GeminiRequestor requestor = new Gemini.Net.GeminiRequestor();

            var ipAddress = DnsCache.Global.GetLookup(hostname);
            string ret = "";
            if (ipAddress != null)
            {

                var resp = requestor.Request(robotsUrl, ipAddress);

                if (Crawler != null && resp.IsSuccess)
                {
                    Crawler.ProcessRequestResponse(resp);
                }
                ret = (resp.IsSuccess && resp.HasBody) ?
                    resp.BodyText :
                    "";
            }
            return ret;
        }
    }
}