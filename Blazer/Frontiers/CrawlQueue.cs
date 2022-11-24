using System;
using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Blazer.Frontiers;

/// <summary>
/// Manages our queue of URLs to crawl. URL that have already been added are ignored
/// </summary>
public class CrawlQueue : IUrlFrontier
{
    object locker;

    /// <summary>
    /// our queue of URLs to crawl
    /// </summary>
    Queue<GeminiUrl> queue;

    int stopAfterUrls = int.MaxValue;
    int totalUrlsProcessed = 0;

    public int Count
        => queue.Count;

    public CrawlQueue(int stopAfter = 10000)
    {
        queue = new Queue<GeminiUrl>();
        locker = new object();
        stopAfterUrls = stopAfter;
    }

    public void AddUrl(GeminiUrl url)
        => queue.Enqueue(url);

    public GeminiUrl GetUrl()
    {
        lock (locker)
        {
            totalUrlsProcessed++;
            if (totalUrlsProcessed >= stopAfterUrls)
            {
                Console.WriteLine("Crawl Limit reached! Dequeuing no more URLs!");
                return null;
            }
            return (queue.Count > 0) ? queue.Dequeue() : null;
        }
    }
}