using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Models;

/// <summary>
/// Current searchable representation for a normalized Gemini URL.
/// One row per normalized URL; updated in-place on re-ingestion.
/// </summary>
[Table("Documents")]
[Index(nameof(StatusCode))]
[Index(nameof(Host))]
public class DocumentRecord
{
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// FK into UrlRegistry. Nullable only because SQLite's ON DELETE SET NULL behaviour
    /// prevents orphan Documents when a UrlRegistry row is removed.
    /// In practice every Document has a valid UrlRegistryId.
    /// </summary>
    public long? UrlRegistryId { get; set; }

    /// <summary>The URL as it should appear in search results. Also used as the FTS5 content-table column.</summary>
    [MaxLength(1024)]
    [Required]
    public string CanonicalUrl { get; set; } = "";

    /// <summary>Hostname component of CanonicalUrl, denormalized from UrlRegistry for site: filter performance.</summary>
    [MaxLength(255)]
    public string Host { get; set; } = "";

    /// <summary>MIME type from the last successful fetch, denormalized from UrlRegistry for filetype: filter performance.</summary>
    [MaxLength(256)]
    public string? LastMimeType { get; set; }

    /// <summary>Page title extracted by <see cref="Kennedy.Data.Parsers.GemText.TitleFinder"/>. Null for non-Gemtext or untitled pages.</summary>
    [MaxLength(512)]
    public string? Title { get; set; }

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

}
