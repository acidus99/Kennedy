using System;
using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Data
{
    public class PlainTextResponse : ParsedResponse, ITextResponse
    {
        public required string? DetectedLanguage { get; set; }

        public bool HasIndexableText => (BodyText.Length > 0);

        /// <summary>
        /// Plain text documents cannot be feeds
        /// </summary>
        public bool IsFeed => false;

        public string? IndexableText => BodyText;

        private int? _lineCount;

        public int LineCount
        {
            get
            {
                if (!_lineCount.HasValue)
                {
                    _lineCount = BodyText.Split('\n').Length;
                }
                return _lineCount.Value;
            }
        }

        public string? Title => null;

        public PlainTextResponse(GeminiResponse resp)
        : base(resp)
        {
            FormatType = ContentType.PlainText;
            DetectedMimeType = "text/plain";
        }

    }
}