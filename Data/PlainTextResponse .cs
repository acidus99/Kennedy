using System;
using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Data
{
    public class PlainTextResponse : ParsedResponse
    {
        public int LineCount { get; set; } = 0;

        public string? DetectedLanguage { get; set; }

        public override bool IsIndexable => (BodyText.Length > 0);

        public PlainTextResponse(GeminiResponse resp)
        : base(resp)
        {
            ContentType = ContentType.PlainText;
        }

    }
}