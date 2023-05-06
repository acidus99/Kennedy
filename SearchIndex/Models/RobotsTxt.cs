using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;

namespace Kennedy.SearchIndex.Models
{
    [Table("RobotsTxts")]
    [PrimaryKey(nameof(Domain), nameof(Port), nameof(Protocol))]
    public class RobotsTxt
    {
        [Required]
        public string Protocol { get; set; }

        [Required]
        public string Domain { get; set; }

        [Required]
        public int Port { get; set; }

        public string Content { get; set; }

        [Required]
        public long SourceUrlID { get; set; }

        public RobotsTxt()
        {
        }

        public RobotsTxt(GeminiUrl url)
        {
            Protocol = url.Protocol;
            Domain = url.Hostname;
            Port = url.Port;
            SourceUrlID = url.ID;
        }

    }
}
