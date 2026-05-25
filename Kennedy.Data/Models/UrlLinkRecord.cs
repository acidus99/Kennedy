using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Models;

/// <summary>
/// Represents a directed hyperlink discovered in a Gemini document.
/// Each row records one source→target URL pair extracted from a link line (<c>=&gt;</c>).
/// The table is fully replaced per source URL each time that source is re-indexed.
/// </summary>
[Table("UrlLinks")]
[Index(nameof(SourceUrlId))]
[Index(nameof(TargetUrlId))]
public class UrlLinkRecord
{
    [Key]
    public long Id { get; set; }

    /// <summary>UrlRegistry.Id of the page containing the link.</summary>
    public long SourceUrlId { get; set; }

    /// <summary>UrlRegistry.Id of the page the link points to.</summary>
    public long TargetUrlId { get; set; }

    /// <summary>True when source and target have different authorities (host:port).</summary>
    public bool IsExternal { get; set; }

    /// <summary>Human-readable label on the link line, trimmed. May be empty.</summary>
    [MaxLength(512)]
    public string LinkText { get; set; } = "";
}
