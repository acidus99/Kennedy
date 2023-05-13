using System;
using System.Linq;
using System.Collections.Generic;
using Gemini.Net;
using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Frontiers;

/// <summary>
/// 
/// </summary>
internal class UrlQueue
{
    object locker = new object();

    PriorityQueue<UrlFrontierEntry, int> queue = new PriorityQueue<UrlFrontierEntry, int>();

    /// <summary>
    /// tracks # of items we have had to a specific authority
    /// </summary>
    Bag<string> AuthorityCounts = new Bag<string>();

    public void AddUrl(UrlFrontierEntry entry)
    {
        var priority = GetPriority(entry);
        lock (locker)
        {
            queue.Enqueue(entry, priority);
        }
    }

    private int GetPriority(UrlFrontierEntry entry)
    {
        int count = AuthorityCounts.Add(entry.Url.Authority);
        count *= 10;

        return 10 * CountDirectories(entry.Url.Path) + count;
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

    public UrlFrontierEntry GetUrl()
    {
        UrlFrontierEntry ret = null;

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
