using System;
using System.Collections.Generic;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    [Table("Favicons")]
    [PrimaryKey(nameof(Protocol), nameof(Domain), nameof(Port))]
    
    public class Favicon
    {
        [Required]
        public string Protocol { get; set; }

        [Required]
        public string Domain { get; set; }

        [Required]
        public int Port { get; set; }

        public string Emoji { get; set; }
        
        public long SourceUrlID { get; set; }

        public IEnumerable<Document> Documents { get; set; }

        public Favicon()
        { }

        public Favicon(GeminiUrl url)
        {
            Protocol = url.Protocol;
            Domain = url.Hostname;
            Port = url.Port;
            SourceUrlID = url.ID;
        }

    }
}
