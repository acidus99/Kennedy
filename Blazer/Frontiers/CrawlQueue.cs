using System;
using System.Collections.Generic;
using Gemini.Net;

namespace Kennedy.Blazer;

/// <summary>
/// Manages our queue of URLs to crawl. URL that have already been added are ignored
/// </summary>
public class CrawlQueue
{
    object locker;

    /// <summary>
    /// our queue of URLs to crawl
    /// </summary>
    Queue<GeminiUrl> queue;

    /// <summary>
    /// Lookup table of URLs we have seen before
    /// </summary>
    Dictionary<string, bool> SeenUrls;

    int stopAfterUrls = int.MaxValue;
    int totalUrlsProcessed = 0;


    public CrawlQueue(int stopAfter = 10000)
    {
        queue = new Queue<GeminiUrl>();
        SeenUrls = new Dictionary<string, bool>();
        locker = new object();
        stopAfterUrls = stopAfter;
    }

    public void EnqueueUrls(List<GeminiUrl> urls)
    {
        int count = 0;
        foreach(var url in urls)
        {
            if(EnqueueUrl(url))
            {
                count++;
            }
        }
        if (count > 0)
        {
            Console.WriteLine($"\tAdded {count} URLs. Queue Length: {Count}");
        }
    }

    /// <summary>
    /// Adds a URL to the queue only if we haven't see it before
    /// </summary>
    /// <param name="url"></param>
    public bool EnqueueUrl(GeminiUrl url)
    {
        lock(locker)
        {
            string normalizedUrl = url.NormalizedUrl;
            if(!SeenUrls.ContainsKey(normalizedUrl))
            {
                Console.WriteLine($"\tAdding new URL '{normalizedUrl}'");
                SeenUrls.Add(normalizedUrl, true);
                queue.Enqueue(url);
                return true;
            }
        }
        return false;
    }

    public GeminiUrl DequeueUrl()
    {
        lock (locker)
        {
            totalUrlsProcessed++;
            if(totalUrlsProcessed >= stopAfterUrls)
            {
                Console.WriteLine("Crawl Limit reached! Dequeuing no more URLs!");
                return null;
            }
            return (queue.Count > 0) ? queue.Dequeue() : null;
        }
    }

    public int Count
        => queue.Count;

}
