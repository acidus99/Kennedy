using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using System.Diagnostics.CodeAnalysis;

namespace Kennedy.SearchIndex.Models
{
    [Table("SecurityTxts")]
    [PrimaryKey(nameof(Domain), nameof(Port), nameof(Protocol))]
    public class SecurityTxt
    {
        [Required]
        public required string Protocol { get; init; }

        [Required]
        public required string Domain { get; init; }

        [Required]
        public required int Port { get; init; }

        [Required]
        public string Content { get; set; } = "";

        [Required]
        public required long SourceUrlID { get; init; }

        public SecurityTxt()
        { }

        [SetsRequiredMembersAttribute]
        public SecurityTxt(GeminiUrl url)
        {
            Protocol = url.Protocol;
            Domain = url.Hostname;
            Port = url.Port;
            SourceUrlID = url.ID;
        }
    }
}
