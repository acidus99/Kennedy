using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    [Keyless]
    public class FullTextSearchResult
    {
        public required long UrlID { get; set; }

        public required string Url { get; set; }

        private GeminiUrl? _geminiUrl;

        public GeminiUrl GeminiUrl
        {
            get
            {
                if(_geminiUrl == null)
                {
                    _geminiUrl = new GeminiUrl(Url);
                }
                return _geminiUrl;
            }
        }

        [NotMapped]
        public string? Favicon { get; set; }

        public required int BodySize { get; set; }
        public required string? Title { get; set; }
        public required string Snippet { get; set; }

        public required string? DetectedLanguage { get; set; }
        public required int? LineCount { get; set; }
    }
}
