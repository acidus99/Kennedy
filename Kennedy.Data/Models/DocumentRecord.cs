using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Models;

/// <summary>
/// Current searchable representation for a normalized Gemini URL.
/// </summary>
[Table("Documents")]
[Index(nameof(NormalizedUrl), IsUnique = true)]
[Index(nameof(IsSearchable))]
[Index(nameof(StatusCode))]
public class DocumentRecord
{
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Best-effort pointer to UrlRegistry when available.
    /// This may be null during batch ingestion before UrlRegistry rows are flushed.
    /// </summary>
    public long? UrlRegistryId { get; set; }

    [MaxLength(1024)]
    [Required]
    public string NormalizedUrl { get; set; } = "";

    [MaxLength(1024)]
    [Required]
    public string CanonicalUrl { get; set; } = "";

    [MaxLength(512)]
    public string? Title { get; set; }

    [Required]
    public string Content { get; set; } = "";

    [MaxLength(256)]
    public string? MimeType { get; set; }

    [MaxLength(256)]
    public string? DetectedMimeType { get; set; }

    public int StatusCode { get; set; }

    [MaxLength(128)]
    public string? BodyHash { get; set; }

    [MaxLength(128)]
    public string? ResponseHash { get; set; }

    public DateTime LastIndexedUtc { get; set; }

    public bool IsSearchable { get; set; }

    public bool IsBodyTruncated { get; set; }

    public int BodySize { get; set; }

    public int OutboundLinks { get; set; }

    public bool IsFeed { get; set; }

    public int? LineCount { get; set; }

    [MaxLength(8)]
    public string? Language { get; set; }

    [MaxLength(8)]
    public string? DetectedLanguage { get; set; }

    public ContentType ContentType { get; set; } = ContentType.Unknown;

    public DocumentImageRecord? Image { get; set; }
}
