using Gemini.Net;
using Kennedy.Data.Utils;

namespace Kennedy.Data;

/// <summary>
/// A <see cref="GeminiResponse"/> enriched with parsed metadata produced by the parsing pipeline.
/// Subclasses (<see cref="GemTextResponse"/>, <see cref="PlainTextResponse"/>, <see cref="ImageResponse"/>)
/// carry format-specific data. The base class is used for redirect, error, and unknown binary responses.
/// </summary>
public class ParsedResponse : GeminiResponse
{
    /// <summary>High-level format category determined by the parsing pipeline.</summary>
    public ContentType FormatType { get; set; } = ContentType.Unknown;

    /// <summary>MIME type detected from file magic bytes; may differ from the header-declared MimeType.</summary>
    public string? DetectedMimeType { get; set; }

    /// <summary>All outbound links found in this response (populated for Gemtext; redirect target for redirects).</summary>
    public List<FoundLink> Links { get; set; }

    /// <summary>
    /// True for system-initiated URLs (<c>/robots.txt</c>, <c>/favicon.txt</c>, <c>/.well-known/security.txt</c>).
    /// Used to suppress crawler housekeeping responses from appearing in full-text search results.
    /// </summary>
    public bool IsProactiveRequest
        => UrlUtility.IsProactiveUrl(RequestUrl.Url);

    public ParsedResponse(GeminiResponse baseResponse)
        : base(baseResponse.RequestUrl)
    {
        Links = new List<FoundLink>();

        StatusCode = baseResponse.StatusCode;
        Meta = baseResponse.Meta;
        RemoteAddress = baseResponse.RemoteAddress;
        RequestSent = baseResponse.RequestSent;
        ResponseReceived = baseResponse.ResponseReceived;

        //body properties
        BodyBytes = baseResponse.BodyBytes;
        IsBodyTruncated = baseResponse.IsBodyTruncated;

        //parsed items if there is a body
        MimeType = baseResponse.MimeType;
        Charset = baseResponse.Charset;
        Language = baseResponse.Language;

        //timers
        ConnectTime = baseResponse.ConnectTime;
        DownloadTime = baseResponse.DownloadTime;
    }
}