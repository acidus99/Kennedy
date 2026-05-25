using Gemini.Net;

namespace Kennedy.Search.Models;

/// <summary>
/// A single result row from an image search, projected from the UrlRegistry table.
/// Images are found by searching the FilesFts virtual table, which indexes link text
/// and URL path tokens for non-text URLs.
/// </summary>
public sealed class ImageSearchResult
{
    /// <summary>The normalized URL pointing to the image resource.</summary>
    public required string Url { get; init; }

    /// <summary>Context snippet from the FilesFts index (link text / path tokens around the match).</summary>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>Image format name (e.g. "Png", "Jpeg") detected by ImageSharp.</summary>
    public string? ImageType { get; init; }

    /// <summary>Image width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Uncompressed body size in bytes.</summary>
    public int BodySize { get; init; }

    /// <summary>True when the crawler received a truncated body for this image.</summary>
    public bool IsBodyTruncated { get; init; }

    /// <summary>Convenience accessor that wraps <see cref="Url"/> in a <see cref="GeminiUrl"/>.</summary>
    public GeminiUrl GeminiUrl => new(Url);
}
