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
        int priority = AuthorityCounts.Add(entry.Url.Authority);

        if(entry.IsProactive)
        {
            return 100;
        }

        if(IsGemtextRequest(entry.Url) || IsTextRequest(entry.Url))
        {
            return priority;
        }

        //for non-text responses, delay it
        priority +=5000;

        return priority;
    }

    private bool IsGemtextRequest(GeminiUrl url)
        => (url.Filename == "" || url.FileExtension == "gmi");

    private bool IsTextRequest(GeminiUrl url)
        => (url.FileExtension == "txt");

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
