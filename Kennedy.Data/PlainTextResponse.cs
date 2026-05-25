using Gemini.Net;

namespace Kennedy.Data;

/// <summary>
/// Parsed result for a <c>text/plain</c> response.
/// Produced by <see cref="Parsers.PlainTextResponseParser"/>; implements <see cref="ITextResponse"/>.
/// Plain text responses for proactive (system-initiated) URLs like robots.txt are not indexed.
/// </summary>
public class PlainTextResponse : ParsedResponse, ITextResponse
{
    /// <summary>ISO 639-1 language code detected by NTextCat, or null if content is too short.</summary>
    public required string? DetectedLanguage { get; set; }

    /// <summary>
    /// True when the body is non-empty AND this is not a proactive system URL (robots.txt, etc.).
    /// Prevents crawler housekeeping responses from polluting the search index.
    /// </summary>
    public bool HasIndexableText => !IsProactiveRequest && (BodyText.Length > 0);

    /// <summary>Plain text documents cannot be feeds.</summary>
    public bool IsFeed => false;

    /// <summary>The raw decoded body text — no transformation applied.</summary>
    public string? IndexableText => BodyText;

    private int? _lineCount;

    /// <summary>Number of lines in the body; computed lazily and cached.</summary>
    public int LineCount
    {
        get
        {
            if (!_lineCount.HasValue)
            {
                _lineCount = BodyText.Split('\n').Length;
            }
            return _lineCount.Value;
        }
    }

    /// <summary>Plain text responses have no structured title.</summary>
    public string? Title => null;

    public PlainTextResponse(GeminiResponse resp)
    : base(resp)
    {
        FormatType = ContentType.PlainText;
        DetectedMimeType = "text/plain";
    }
}