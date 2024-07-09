using System;
namespace Kennedy.Crawler.Crawling;

/// <summary>
/// Tracks how a request was handled
/// This information will be used to more accurately prune/update the search index.
/// </summary>
public enum SkippedReason
{
    /// <summary>
    /// We have a response for this request.
    /// This may be the actual response from a sent request
    /// Or contain special information about why the request failed (terminal connectivity error, etc.)
    /// </summary>
    NotSkipped,

    /// <summary>
    /// We didn't make the request because it was blocked by robots.txt.
    /// This URL should be removed from our Search index if it exists
    /// </summary>
    SkippedForRobots,

    /// <summary>
    /// We skipped this request due to ongoing (and temporary) connectivity issue with the host.
    /// We don't need to update our LastVisit meta data for this request
    /// </summary>
    SkippedForConnectivity,
}

