using System;
using Gemini.Net;

namespace Kennedy.SearchIndex.Engines
{
    public class ImageSearchResult
    {
        public GeminiUrl Url { get; set; }
        public int BodySize { get; set; }
        public string Snippet { get; set; }
        public long DBDocID { get; set; }

        public string ImageType { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public string Favicon { get; set; }
        public bool BodySaved { get; set; }
    }
}
