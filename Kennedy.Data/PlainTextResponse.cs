using Gemini.Net;

namespace Kennedy.Data;

public class PlainTextResponse : ParsedResponse, ITextResponse
{
    public required string? DetectedLanguage { get; set; }

    //text files that are not proactive requests can be indexed
    public bool HasIndexableText => !IsProactiveRequest && (BodyText.Length > 0);

    /// <summary>
    /// Plain text documents cannot be feeds
    /// </summary>
    public bool IsFeed => false;

    public string? IndexableText => BodyText;

    private int? _lineCount;

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

    public string? Title => null;

    public PlainTextResponse(GeminiResponse resp)
    : base(resp)
    {
        FormatType = ContentType.PlainText;
        DetectedMimeType = "text/plain";
    }
}