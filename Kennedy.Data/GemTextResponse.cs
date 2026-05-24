using Gemini.Net;

namespace Kennedy.Data;

public class GemTextResponse : ParsedResponse, ITextResponse
{
    public required string? DetectedLanguage { get; set; }

    public bool HasIndexableText => (IndexableText?.Length > 0);

    public string? IndexableText { get; set; }

    public bool IsFeed { get; set; }

    public required int LineCount { get; set; }

    public string? Title { get; set; }

    public IEnumerable<String> Mentions = new List<string>();

    public IEnumerable<String> HashTags = new List<string>();

    public GemTextResponse(GeminiResponse resp)
    : base(resp)
    {
        FormatType = ContentType.Gemtext;
        DetectedMimeType = "text/gemini";
    }
}