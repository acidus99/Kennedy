using System;
using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    public class ImageSearchResult
    {
        public required long UrlID { get; set; }

        public required GeminiUrl Url { get; set; }
        public required int BodySize { get; set; }
        public required string Snippet { get; set; }

        public required string ImageType { get; set; }
        public required int Width { get; set; }
        public required int Height { get; set; }

        public string? Favicon { get; set; }
    }
}
