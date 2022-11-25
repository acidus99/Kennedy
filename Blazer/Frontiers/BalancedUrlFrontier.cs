﻿using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Gemini.Net;

using Kennedy.Blazer.Dns;
using Kennedy.Blazer.Utils;

namespace Kennedy.Blazer.Frontiers;

/// <summary>
/// Manages our queue of URLs to crawl
/// </summary>
public class BalancedUrlFrontier : IUrlFrontier
{
    object locker;

    /// <summary>
    /// our queue of URLs to crawl
    /// </summary>
    UrlQueue [] queues;

    int totalWorkerThreads;
    ThreadSafeCounter totalUrls;

    public int Count => GetCount();

    public int Total => totalUrls.Count;

    public string ModuleName => "Balanced Url Frontier";

    public BalancedUrlFrontier(int totalWorkers)
    {
        locker = new object();
        totalWorkerThreads = totalWorkers;
        totalUrls = new ThreadSafeCounter();

        queues = new UrlQueue[totalWorkerThreads];
        for(int i = 0; i< totalWorkerThreads; i++)
        {
            queues[i] = new UrlQueue();
        }
    }

    private int queueForUrl(GeminiUrl url)
    {
        //we are trying to avoid adding URLs that are all served by the same
        //system from being dumped into different buckets, where we then overwhelm
        //that server. Basically Flounder, since all the subdomains are served by the same system

        //try and look up the ip address for this host. If we don't get one,
        //fall back to using the hostname.

        string address = url.Authority;
        //string address = DnsCache.Global.GetLookup(url.Hostname);
        int hash = (address != null) ? address.GetHashCode() : url.Hostname.GetHashCode();

        return Math.Abs(hash) % totalWorkerThreads;
    }

    public void AddUrl(GeminiUrl url)
    {
        totalUrls.Increment();
        int queueID = queueForUrl(url);
        queues[queueID].AddUrl(url);
    }

    private int GetCount()
    {
        int totalCount = 0;
        for(int i=0;i<totalWorkerThreads; i++)
        {
            totalCount += queues[i].Count;
        }
        return totalCount;
    }

    public GeminiUrl GetUrl(int crawlerID)
        => queues[crawlerID].GetUrl();

    public string GetStatus()
        => $"Queue Size:\t{Count}";
}