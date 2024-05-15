using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Gemini.Net;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Models;

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