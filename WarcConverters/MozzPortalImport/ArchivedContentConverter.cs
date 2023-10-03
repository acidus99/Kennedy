namespace Kennedy.WarcConverters.MozzPortalImport;

using System.Net;
using Gemini.Net;

//Converts archive 
public class ArchivedContentConverter
{
    HttpRequestor httpRequestor = new HttpRequestor();

    public HttpResponseMessage GetResponse(Uri url)
    {
        return httpRequestor.SendRequest(url);
    }

    public ArchivedContent Convert(WaybackUrl waybackUrl)
    {
        var response = GetResponse(waybackUrl.Url);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                return ParseHttpSuccess(waybackUrl, response);

            default:
                throw new ApplicationException($"Unhandled Status Code {response.StatusCode}");
        }
    }

    private ArchivedContent ParseHttpSuccess(WaybackUrl waybackUrl, HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType == null || response.Content.Headers.ContentType.MediaType == null)
        {
            throw new ApplicationException("Response does not have a Content-Type header");
        }

        string mimeType = response.Content.Headers.ContentType.MediaType;

        if (waybackUrl.IsRawRequest)
        {
            if (mimeType == "text/plain")
            {
                throw new ApplicationException($"Unhandled Proxied Content: Raw Request with text/plain response");
            }
        }

        switch (mimeType)
        {
            case "text/html":
                return ParseHtmlResponse(waybackUrl, ReadAllText(response.Content));

            default:
                return ParseBinaryResponse(waybackUrl, response.Content);
        }
    }

    private string ReadAllText(HttpContent content)
    {
        return content.ReadAsStringAsync().Result;
    }

    private ArchivedContent ParseHtmlResponse(WaybackUrl waybackUrl, string html)
    {
        var mozzHtmlConverter = new MozzHtmlConverter(waybackUrl, html);

        return mozzHtmlConverter.GetContent();
    }

    private ArchivedContent ParseBinaryResponse(WaybackUrl waybackUrl, HttpContent content)
    {
        string responseLine = $"20 {content.Headers.ContentType!.MediaType}";

        GeminiResponse geminiResponse = new GeminiResponse(waybackUrl.GetProxiedUrl(), responseLine);
        geminiResponse.BodyBytes = content.ReadAsByteArrayAsync().Result;
        return new ArchivedContent
        {
            Url = waybackUrl,
            GeminiResponse = geminiResponse
        };
    }

}