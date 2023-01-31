using System;
using System.Linq;
using System.Collections.Generic;
using Gemini.Net;
using Kennedy.Blazer.Utils;

namespace Kennedy.Blazer.Frontiers;

/// <summary>
/// 
/// </summary>
internal class UrlQueue
{
    object locker = new object();

    PriorityQueue<GeminiUrl, int> queue = new PriorityQueue<GeminiUrl, int>();

    /// <summary>
    /// tracks # of items we have had to a specific authority
    /// </summary>
    Bag<string> AuthorityCounts = new Bag<string>();

    public void AddUrl(GeminiUrl url)
    {
        var priority = GetPriority(url);
        lock (locker)
        {
            queue.Enqueue(url, priority);
        }
    }

    private int GetPriority(GeminiUrl url)
    {
        int count = AuthorityCounts.Add(url.Authority);
        count *= 10;

        return 10 * CountDirectories(url.Path) + count;
    }

    private int CountDirectories(string path)
    {
        int ret = 0;
        foreach(char c in path)
        {
            if(c == '/')
            {
                ret++;
            }
        }
        //-1 because every URL starts with a /
        return ret - 1;
    }

    public GeminiUrl GetUrl()
    {
        GeminiUrl ret = null;

        lock (locker)
        {
            ret = (queue.Count > 0) ?
                    queue.Dequeue() :
                    null;
        }
        return ret;
    }

    public int Count
        => queue.Count;

}
