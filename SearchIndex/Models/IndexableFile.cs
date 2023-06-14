using System;
using Gemini.Net;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Models
{
    [Keyless]
    internal class IndexableFile
    {
        public required long UrlID { get; set; }

        public required string Url { get; set; }

        public string? LinkText { get; set; }
    }
}

