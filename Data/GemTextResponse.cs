using System;
using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Data
{
    public class GemTextResponse : ParsedResponse
    {
        public int LineCount { get; set; } = 0;

        public string? DetectedLanguage { get; set; }

        public string? Title { get; set; }

        public override bool IsIndexable => (FilteredBody.Length > 0);

        public string FilteredBody { get; set; } = "";

        public IEnumerable<String> Mentions = new List<string>();

        public IEnumerable<String> HashTags = new List<string>();

        public GemTextResponse(GeminiResponse resp)
        : base(resp)
        {
            ContentType = ContentType.Gemtext;
        }

    }
}