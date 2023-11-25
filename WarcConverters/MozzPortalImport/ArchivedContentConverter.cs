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

            case HttpStatusCode.NotFound:
                //nothing to extract
                return new ArchivedContent
                {
                    Url = waybackUrl
                };

            default:
                throw new ApplicationException($"Unhandled Status Code {response.StatusCode}");
        }
    }

    private ArchivedContent ParseHttpSuccess(WaybackUrl waybackUrl, HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType == null || response.Content.Headers.ContentType.MediaType == null)
        {
            //there are 7 odd files that made mozz's portal send an invalid content type (2 semi colons in it).
            //force it to be treated as text/gemini
            return ParseBinaryResponse(waybackUrl, "text/gemini", response.Content);
        }

        string mimeType = response.Content.Headers.ContentType.MediaType;

        if (waybackUrl.IsRawRequest)
        {
            if (mimeType == "text/plain")
            {
                //force it to be treated as text/gemini
                return ParseBinaryResponse(waybackUrl, "text/gemini", response.Content);
            }
        }

        switch (mimeType)
        {
            case "text/html":
                return ParseHtmlResponse(waybackUrl, ReadAllText(response.Content));

            default:
                return ParseBinaryResponse(waybackUrl, mimeType, response.Content);
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

    private ArchivedContent ParseBinaryResponse(WaybackUrl waybackUrl, string mimetype, HttpContent content)
    {
        string responseLine = $"20 {mimetype}";
        GeminiResponse geminiResponse = new GeminiResponse(waybackUrl.GetProxiedUrl(), responseLine);
        geminiResponse.BodyBytes = content.ReadAsByteArrayAsync().Result;
        geminiResponse.RequestSent = waybackUrl.Captured;
        geminiResponse.ResponseReceived = waybackUrl.Captured;
        return new ArchivedContent
        {
            Url = waybackUrl,
            GeminiResponse = geminiResponse
        };
    }
}