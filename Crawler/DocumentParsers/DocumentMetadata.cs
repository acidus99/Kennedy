using System;
using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Crawler.DocumentParsers
{
    public class DocumentMetadata
    {
        public bool IsIndexable => (FilteredBody.Length > 0);

        public List<FoundLink> Links { get; set; } = new List<FoundLink>();

        public int LineCount { get; set; } = 0;

        public string Language { get; set; } = "";

        public string Title { get; set; } = "";

        public string FilteredBody { get; set; } = "";

        public DocumentMetadata()
        {

        }

        public DocumentMetadata(FoundLink link)
        {
            Links.Add(link);
        }

    }
}
