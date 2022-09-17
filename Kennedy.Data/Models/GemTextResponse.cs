using System;
using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Data.Models
{
    public class GemTextResponse : AbstractResponse
    {
        public int LineCount { get; set; } = 0;

        public string Language { get; set; } = "";

        public string Title { get; set; } = "";

        public bool IsIndexable => (FilteredBody.Length > 0);

        public string FilteredBody { get; set; } = "";
    }
}