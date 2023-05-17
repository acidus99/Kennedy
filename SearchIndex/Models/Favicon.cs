using System;
using System.Collections.Generic;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using System.Diagnostics.CodeAnalysis;

namespace Kennedy.SearchIndex.Models
{
    [Table("Favicons")]
    [PrimaryKey(nameof(Protocol), nameof(Domain), nameof(Port))]
    
    public class Favicon
    {
        [Required]
        public required string Protocol { get; init; }

        [Required]
        public required string Domain { get; init; }

        [Required]
        public required int Port { get; init; }

        public string Emoji { get; set; } = "";
        
        public required long SourceUrlID { get; init; }

        public IEnumerable<Document>? Documents{ get; set; }

        public Favicon()
        { }

        [SetsRequiredMembersAttribute]
        public Favicon(GeminiUrl url)
        {
            Protocol = url.Protocol;
            Domain = url.Hostname;
            Port = url.Port;
            SourceUrlID = url.ID;
            Emoji = "";
        }

    }
}
