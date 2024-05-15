using System;

namespace Kennedy.SearchIndex;

public class SearchStats
{
    /// <summary>
    /// Total number of domains 
    /// </summary>
    public long Domains { get; set; }

    /// <summary>
    /// Total number of all Urls
    /// </summary>
    public long Urls { get; set; }

    /// <summary>
    /// Total number of all Urls with a 2x response bode
    /// </summary>
    public long SuccessUrls { get; set; }

    public DateTime LastUpdated { get; set; }
}