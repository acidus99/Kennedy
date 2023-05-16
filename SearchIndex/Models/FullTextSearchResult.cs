using System;
using Microsoft.EntityFrameworkCore;
using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    [Keyless]
    public class FullTextSearchResult
    {
        public required long UrlID { get; set; }

        public required GeminiUrl Url { get; set; }
        public required int BodySize { get; set; }
        public required string? Title { get; set; }
        public required string Snippet { get; set; }

        public required string? Language { get; set; }
        public required int? LineCount { get; set; }

        public string? Favicon { get; set; }

        public int ExternalInboundLinks { get; set; }

        #region meta data for debugging

        //score of our FTS
        public required double FtsRank { get; set; }
        //score of our popularity ranker
        public required double PopRank { get; set; }
        public required double TotalRank { get; set; }
        #endregion

    }
}
