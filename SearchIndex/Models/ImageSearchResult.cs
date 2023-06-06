using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    [Keyless]
    public class ImageSearchResult
    {
        public required long UrlID { get; set; }

        public required string Url { get; set; }

        private GeminiUrl? _geminiUrl;

        public GeminiUrl GeminiUrl
        {
            get
            {
                if (_geminiUrl == null)
                {
                    _geminiUrl = new GeminiUrl(Url);
                }
                return _geminiUrl;
            }
        }

        public required int BodySize { get; set; }
        public required string Snippet { get; set; }

        public required string ImageType { get; set; }
        public required int Width { get; set; }
        public required int Height { get; set; }

        [NotMapped]
        public string? Favicon { get; set; }
    }
}
