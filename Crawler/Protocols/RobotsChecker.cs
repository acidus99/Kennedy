﻿using System.Collections.Concurrent;
using Gemini.Net;
using Kennedy.Crawler.Crawling;
using Kennedy.Crawler.Dns;
using Kennedy.Data.RobotsTxt;

namespace Kennedy.Crawler.Protocols;

/// <summary>
/// returns if a URL is allowed or not, based on it's Robots.txt
/// //grabs new Robots.txt files ondemand
/// </summary>
public class RobotsChecker
{
    public static RobotsChecker Global = new RobotsChecker();

    ConcurrentDictionary<string, RobotsTxtFile?> Cache;

    public RobotsChecker()
    {
        Cache = new ConcurrentDictionary<string, RobotsTxtFile?>();
    }

    public IWebCrawler? Crawler { get; set; } = null;

    public bool IsAllowed(string url)
        => IsAllowed(new GeminiUrl(url));

    public bool IsAllowed(GeminiUrl url)
    {
        //check if its in our cache

        var key = GetCacheKey(url);

        RobotsTxtFile? robots = null;

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

    /// <summary>
    /// Checks if a URL is a allowed, without fetching Robots.txt if need be
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public bool IsAllowedOffline(GeminiUrl url)
    {
        var key = GetCacheKey(url);

        RobotsTxtFile? robots;
        if (Cache.TryGetValue(key, out robots))
        {
            return robots != null && robots.IsPathAllowed("indexer", url.Path);
        }

        //nothing explicitly telling me no, so allow it
        return true;
    }

    private string GetCacheKey(GeminiUrl url)
        => url.Authority;

    /// <summary>
    /// Downloads the Robots.txt file for a host, parses it, and adds it to the cache
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hostname"></param>
    /// <param name="port"></param>
    private RobotsTxtFile? LoadRobotsIntoCache(string key, string hostname, int port)
    {
        try
        {
            var contents = FetchRobots(hostname, port);
            RobotsTxtParser parser = new RobotsTxtParser();
            RobotsTxtFile robots = parser.Parse(contents);
            if (robots.HasValidRules)
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
        var robotsUrl = RobotsTxtParser.CreateRobotsUrl("gemini", hostname, port);

        GeminiRequestor requestor = new GeminiRequestor();

        var ipAddress = DnsCache.Global.GetLookup(hostname);
        string ret = "";
        if (ipAddress != null)
        {
            var requestUrl = new GeminiUrl(robotsUrl + "?kennedy-crawler");

            GeminiResponse resp = requestor.Request(requestUrl, ipAddress);

            if (Crawler != null)
            {
                // reset the URL to remove the special note we sent about our crawler
                resp.RequestUrl = new GeminiUrl(robotsUrl);
                Crawler.ProcessRobotsResponse(resp);
            }
            ret = (resp.IsSuccess && resp.HasBody) ?
                resp.BodyText :
                "";
        }
        return ret;
    }
}
