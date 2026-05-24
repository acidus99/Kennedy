using Gemini.Net;

namespace Kennedy.Search.Models;

public sealed class TextSearchResult
{
    public required string Url { get; init; }
    public string? Title { get; init; }
    public string Snippet { get; init; } = string.Empty;
    public string? MimeType { get; init; }
    public string? DetectedLanguage { get; init; }
    public int? LineCount { get; init; }
    public int BodySize { get; init; }
    public bool IsBodyTruncated { get; init; }

    public GeminiUrl GeminiUrl => new(Url);
}
