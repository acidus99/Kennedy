using System;
using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Data
{
    public class GemTextResponse : ParsedResponse
    {
        public int LineCount { get; set; } = 0;

        public string? Language { get; set; }

        public string? Title { get; set; }

        public bool IsIndexable => (FilteredBody.Length > 0);

        public string FilteredBody { get; set; } = "";

        public GemTextResponse(GeminiResponse resp)
        : base(resp)
        { }

    }
}