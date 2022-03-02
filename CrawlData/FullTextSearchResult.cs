using System;
using Gemini.Net;

namespace Kennedy.CrawlData
{
    public class FullTextSearchResult
    {
        public GeminiUrl Url { get; set; }
        public int BodySize { get; set; }
        public string Title { get; set; }
        public string Snippet { get; set; }
        public long DBDocID { get; set; }

        public string Language { get; set; }
        public int LineCount { get; set; }

        public string Favicon { get; set; }

        public bool BodySaved { get; set; }

        public int ExternalInboundLinks { get; set; }

        #region meta data for debugging

        //score of our FTS
        public double FtsRank { get; set; }
        //score of our popularity ranker
        public double PopRank { get; set; }
        public double TotalRank { get; set; }
        #endregion

    }
}
