using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    [Table("RobotsTxts")]
    public class RobotsTxt
    {
        [Required]
        public string Protocol { get; set; }

        [Required]
        public string Domain { get; set; }

        [Required]
        public int Port { get; set; }

        public string Content { get; set; }

        public long UrlID { get; set; }

        public Document SourceUrl { get; set; }
    }
}
