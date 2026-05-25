using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Models;

/// <summary>
/// Current searchable representation for a normalized Gemini URL.
/// One row per normalized URL; updated in-place on re-ingestion.
/// The FTS5 index (<c>DocumentsFts</c>) is kept in sync by SQLite triggers on this table.
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

    /// <summary>Canonical normalized URL string. Unique across the table.</summary>
    [MaxLength(1024)]
    [Required]
    public string NormalizedUrl { get; set; } = "";

    /// <summary>The URL as it should appear in search results (currently same as NormalizedUrl).</summary>
    [MaxLength(1024)]
    [Required]
    public string CanonicalUrl { get; set; } = "";

    /// <summary>Page title extracted by <see cref="Kennedy.Data.Parsers.GemText.TitleFinder"/>. Null for non-Gemtext or untitled pages.</summary>
    [MaxLength(512)]
    public string? Title { get; set; }

    /// <summary>Full searchable text fed into DocumentsFts. Empty for non-indexable responses.</summary>
    [Required]
    public string Content { get; set; } = "";

    /// <summary>MIME type as reported by the Gemini response header (e.g. "text/gemini").</summary>
    [MaxLength(256)]
    public string? MimeType { get; set; }

    /// <summary>MIME type as detected by <see cref="Kennedy.Data.Parsers.BinaryParser"/> via file magic bytes.</summary>
    [MaxLength(256)]
    public string? DetectedMimeType { get; set; }

    /// <summary>Gemini protocol status code from the response (e.g. 20, 51).</summary>
    public int StatusCode { get; set; }

    /// <summary>Hash of the raw response body bytes. Used to detect body-only changes.</summary>
    [MaxLength(128)]
    public string? BodyHash { get; set; }

    /// <summary>
    /// Hash covering both the status/meta line and body.
    /// When this matches the stored value on re-ingestion, the document fields are NOT rewritten,
    /// preventing spurious FTS index updates.
    /// </summary>
    [MaxLength(128)]
    public string? ResponseHash { get; set; }

    /// <summary>UTC timestamp of the most recent indexing pass that touched this row.</summary>
    public DateTime LastIndexedUtc { get; set; }

    /// <summary>True when this document should appear in full-text search results.</summary>
    public bool IsSearchable { get; set; }

    /// <summary>True when the crawler received a truncated body (response was cut off at the size limit).</summary>
    public bool IsBodyTruncated { get; set; }

    /// <summary>Uncompressed body size in bytes.</summary>
    public int BodySize { get; set; }

    /// <summary>Count of outbound links found in this document.</summary>
    public int OutboundLinks { get; set; }

    /// <summary>True if the document was identified as a Gemfeed (2+ links with ISO 8601 date prefixes).</summary>
    public bool IsFeed { get; set; }

    /// <summary>Total number of lines in the raw Gemtext body. Null for non-Gemtext.</summary>
    public int? LineCount { get; set; }

    /// <summary>Language tag declared in the Gemini response header (rarely set).</summary>
    [MaxLength(8)]
    public string? Language { get; set; }

    /// <summary>ISO 639-1 language code detected by NTextCat. Null when content is too short (&lt;150 chars).</summary>
    [MaxLength(8)]
    public string? DetectedLanguage { get; set; }

    /// <summary>High-level content category (Gemtext, PlainText, Image, Binary, Unknown).</summary>
    public ContentType ContentType { get; set; } = ContentType.Unknown;

    /// <summary>Navigation property to image metadata. Non-null only when ContentType is Image.</summary>
    public DocumentImageRecord? Image { get; set; }
}
