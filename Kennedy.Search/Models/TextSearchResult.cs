using Gemini.Net;

namespace Kennedy.Search.Models;

/// <summary>
/// A single result row from a text document search, projected from the Documents table.
/// The <see cref="Snippet"/> is either an FTS5 <c>snippet()</c> fragment (when an FTS query is present)
/// or the first 180 characters of the document body.
/// </summary>
public sealed class TextSearchResult
{
    /// <summary>The canonical URL of the matching document.</summary>
    public required string Url { get; init; }

    /// <summary>Extracted page title, or null when no heading or preformatted alt text was found.</summary>
    public string? Title { get; init; }

    /// <summary>Context snippet around the query match, with query terms wrapped in <c>[brackets]</c>.</summary>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>MIME type as declared in the Gemini response header.</summary>
    public string? MimeType { get; init; }

    /// <summary>ISO 639-1 language code detected by NTextCat, or null.</summary>
    public string? DetectedLanguage { get; init; }

    /// <summary>Total number of lines in the Gemtext body. Null for plain text documents.</summary>
    public int? LineCount { get; init; }

    /// <summary>Uncompressed body size in bytes.</summary>
    public int BodySize { get; init; }

    /// <summary>True when the crawler received a truncated body for this document.</summary>
    public bool IsBodyTruncated { get; init; }

    /// <summary>Convenience accessor that wraps <see cref="Url"/> in a <see cref="GeminiUrl"/>.</summary>
    public GeminiUrl GeminiUrl => new(Url);
}
