namespace Kennedy.Data;

/// <summary>
/// High-level format classification for a crawled response body.
/// Stored on <see cref="Models.DocumentRecord"/> and set by the parsing pipeline.
/// </summary>
public enum ContentType : int
{
    /// <summary>Format could not be determined (e.g. no body, unrecognized type).</summary>
    Unknown = 0,

    /// <summary>Gemini text format (<c>text/gemini</c>). Supports headings, links, preformatted blocks.</summary>
    Gemtext = 1,

    /// <summary>A recognized image format (PNG, JPEG, GIF, etc.) detected via file magic bytes.</summary>
    Image = 2,

    /// <summary>Non-image binary format (PDF, archive, executable, etc.).</summary>
    Binary = 3,

    /// <summary>Plain UTF-8 text (<c>text/plain</c>).</summary>
    PlainText = 4,
}