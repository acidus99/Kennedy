using System;
using System.Collections.Generic;
using Gemini.Net;
using Kennedy.Blazer.Utils;

namespace Kennedy.Blazer.Crawling;

public class SeenContentTracker
{
    Dictionary<uint, bool> seenHashes;
    object locker;

    ThreadSafeCounter duplicateCounter;

    public SeenContentTracker()
    {
        seenHashes = new Dictionary<uint, bool>();
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
        if (resp.HasBody)
        {
            uint hash = resp.BodyHash;
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
