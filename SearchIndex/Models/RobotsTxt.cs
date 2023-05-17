using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using System.Diagnostics.CodeAnalysis;

namespace Kennedy.SearchIndex.Models
{
    [Table("RobotsTxts")]
    [PrimaryKey(nameof(Domain), nameof(Port), nameof(Protocol))]
    public class RobotsTxt
    {
        [Required]
        public required string Protocol { get; set; }

        [Required]
        public required string Domain { get; set; }

        [Required]
        public required int Port { get; set; }

        [Required]
        public required string Content { get; set; }

        [Required]
        public required long SourceUrlID { get; set; }

        public RobotsTxt()
        { }

        [SetsRequiredMembersAttribute]
        public RobotsTxt(GeminiUrl url)
        {
            Protocol = url.Protocol;
            Domain = url.Hostname;
            Port = url.Port;
            SourceUrlID = url.ID;
            Content = "";
        }

    }
}
