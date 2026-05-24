using Gemini.Net;

namespace Kennedy.Search.Models;

public sealed class ImageSearchResult
{
    public required string Url { get; init; }
    public string Snippet { get; init; } = string.Empty;
    public string? ImageType { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int BodySize { get; init; }
    public bool IsBodyTruncated { get; init; }

    public GeminiUrl GeminiUrl => new(Url);
}
