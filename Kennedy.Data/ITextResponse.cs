namespace Kennedy.Data;

/// <summary>
/// Implemented by parsed responses that contain indexable text content.
/// Both <see cref="GemTextResponse"/> and <see cref="PlainTextResponse"/> implement this interface.
/// The <see cref="Services.ResponseStore"/> uses this interface to decide what to write to the Documents table.
/// </summary>
public interface ITextResponse
{
    /// <summary>ISO 639-1 language code detected by NTextCat, or null if content is too short.</summary>
    public string? DetectedLanguage { get; }

    /// <summary>True when <see cref="IndexableText"/> is non-empty and the response should appear in FTS results.</summary>
    public bool HasIndexableText { get; }

    /// <summary>
    /// True when this response looks like a feed of timestamped entries (e.g. a Gemfeed).
    /// Always false for plain text responses.
    /// </summary>
    public bool IsFeed { get; }

    /// <summary>
    /// The text to store in the Documents.Content column and index in FTS5.
    /// For Gemtext: non-preformatted lines with link text extracted from link lines.
    /// For plain text: the raw body.
    /// </summary>
    public string? IndexableText { get; }

    /// <summary>Total number of lines in the raw response body.</summary>
    public int LineCount { get; }

    /// <summary>Page title extracted from the first heading or preformatted alt text. Null if absent.</summary>
    public string? Title { get; }
}