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
        int priority = AuthorityCounts.Add(entry.Url.Authority);
        priority *= 10;

        priority = 10 * CountDirectories(entry.Url.Path) + priority;

        //Force protactive requests to have a higher initial prioroty, so the very early pages of a capsule
        //are queued and fetched ahead of the proactive ones. These early pages have a ton of URLs, so this
        //will fill up queues more quickly and reduces how quickly the crawler "warms up".
        if(entry.IsProactive && priority < 100)
        {
            return 100;
        }
        return priority;
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

    public UrlFrontierEntry? GetUrl()
    {
        UrlFrontierEntry? ret = null;

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
