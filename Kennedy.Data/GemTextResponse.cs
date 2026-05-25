using Gemini.Net;

namespace Kennedy.Data;

/// <summary>
/// Parsed result for a <c>text/gemini</c> response.
/// Produced by <see cref="Parsers.GemTextResponseParser"/>; implements <see cref="ITextResponse"/>
/// so the storage layer can treat it uniformly with other text formats.
/// </summary>
public class GemTextResponse : ParsedResponse, ITextResponse
{
    /// <summary>ISO 639-1 language code detected by NTextCat, or null if content is too short.</summary>
    public required string? DetectedLanguage { get; set; }

    /// <summary>True when <see cref="IndexableText"/> contains at least one character.</summary>
    public bool HasIndexableText => (IndexableText?.Length > 0);

    /// <summary>
    /// Searchable text built from non-preformatted body lines.
    /// Link lines contribute only their human-readable label (not the URL).
    /// </summary>
    public string? IndexableText { get; set; }

    /// <summary>True when 2 or more links have ISO 8601 date prefixes — the Gemfeed convention.</summary>
    public bool IsFeed { get; set; }

    /// <summary>Total number of raw lines in the response body (including preformatted blocks).</summary>
    public required int LineCount { get; set; }

    /// <summary>Title extracted from the first heading or preformatted alt text. Null when absent.</summary>
    public string? Title { get; set; }

    /// <summary>Normalized @-style mentions found in the document text.</summary>
    public IEnumerable<String> Mentions = new List<string>();

    /// <summary>Normalized #-style hashtags found in the document text.</summary>
    public IEnumerable<String> HashTags = new List<string>();

    public GemTextResponse(GeminiResponse resp)
    : base(resp)
    {
        FormatType = ContentType.Gemtext;
        DetectedMimeType = "text/gemini";
    }
}