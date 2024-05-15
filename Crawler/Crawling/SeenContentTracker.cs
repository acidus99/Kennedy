using Gemini.Net;
using Kennedy.Crawler.Utils;

namespace Kennedy.Crawler.Crawling;

public class SeenContentTracker
{
    Dictionary<string, bool> seenHashes;
    object locker;

    ThreadSafeCounter duplicateCounter;

    public SeenContentTracker()
    {
        seenHashes = new Dictionary<string, bool>();
        locker = new object();
        duplicateCounter = new ThreadSafeCounter();
    }

    /// <summary>
    /// checks if we have seen this response body before. records it if we have not
    /// </summary>
    /// <param name="resp"></param>
    /// <returns>if we have seen this resp body before</returns>
    public bool CheckAndRecord(GeminiResponse resp)
    {
        string? hash = resp.BodyHash;
        if (hash != null)
        {
            lock (locker)
            {
                if (!seenHashes.ContainsKey(hash))
                {
                    seenHashes[hash] = true;
                    return false;
                }
                else
                {
                    duplicateCounter.Increment();
                }
            }
        }
        else
        {
            // we want to process all error message, redirect, etc. So if it has no body, we haven't seen it
            return false;
        }
        return true;
    }
}
