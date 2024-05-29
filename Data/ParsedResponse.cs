using Gemini.Net;
using Kennedy.Data.Utils;

namespace Kennedy.Data;

public class ParsedResponse : GeminiResponse
{
    public ContentType FormatType { get; set; } = ContentType.Unknown;

    public string? DetectedMimeType { get; set; }

    public List<FoundLink> Links { get; set; }

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