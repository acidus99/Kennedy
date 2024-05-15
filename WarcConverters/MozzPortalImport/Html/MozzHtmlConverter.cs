using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Gemini.Net;

namespace Kennedy.WarcConverters.MozzPortalImport;

public class MozzHtmlConverter
{
    string Html;

    WaybackUrl WaybackUrl;

    IElement DocumentRoot;

    GeminiResponse response = null!;

    GeminiUrl ProxiedUrl;

    public MozzHtmlConverter(WaybackUrl waybackUrl, string html)
    {
        if (!waybackUrl.IsMozzUrl)
        {
            throw new ArgumentException("Attempting to extract from a wayback URL that is not a URL for the mozz proxy!");
        }

        WaybackUrl = waybackUrl;
        ProxiedUrl = waybackUrl.GetProxiedUrl();
        Html = html;
        DocumentRoot = ParseToRoot(waybackUrl.Url, html);
    }

    public ArchivedContent GetContent()
    {
        if (WaybackUrl.IsCertificateRequest)
        {
            return ParseCertificate();
        }
        else
        {
            return ParseGeminiResponse();
        }
    }

    private ArchivedContent ParseCertificate()
    {
        var pre = DocumentRoot.QuerySelector("pre");
        if (pre == null)
        {
            throw new ApplicationException($"Could not find PRE tag in HTML output for Certificate Request");
        }

        string tlsInfo = pre.TextContent;

        var index = tlsInfo.IndexOf("-----BEGIN CERTIFICATE-----");
        if (index < 0)
        {
            throw new ApplicationException($"Could not find BEGIN CERTIFICATE block inside PRE tag for Certificate Request");
        }

        string cert = tlsInfo.Substring(index);


        return new ArchivedContent
        {
            Url = WaybackUrl,
            Certificate = new X509Certificate2(Encoding.ASCII.GetBytes(cert))
        };
    }

    private ArchivedContent ParseGeminiResponse()
    {
        string? responseLine = GetResponseLine();
        if (responseLine == null)
        {
            //could not find a status code or meta, so there is nothing we can recover
            return new ArchivedContent
            {
                Url = WaybackUrl
            };
        }

        response = new GeminiResponse(ProxiedUrl, responseLine);

        response.RequestSent = WaybackUrl.Captured;
        response.ResponseReceived = WaybackUrl.Captured;
        if (response.IsSuccess)
        {
            return ParseBody(response);
        }

        return new ArchivedContent
        {
            Url = WaybackUrl,
            GeminiResponse = response
        };
    }

    private string? GetResponseLine()
    {
        //grab the first table in the HTML
        var table = DocumentRoot.QuerySelector("table");
        if (table == null)
        {
            throw new ApplicationException($"Could not find a table in the parsed HTML.");
        }

        var cells = table.QuerySelectorAll("td").ToArray();
        if (cells.Length != 4)
        {
            throw new ApplicationException($"Did not find 4 cells in response table! Found {cells.Length}");
        }

        var statusCode = cells[1].TextContent.Trim();

        if (string.IsNullOrEmpty(statusCode))
        {
            return null;
        }

        statusCode = statusCode.Substring(0, 2);
        var meta = cells[3].TextContent.Trim();
        return $"{statusCode} {meta}";
    }

    private ArchivedContent ParseBody(GeminiResponse response)
    {
        switch (response.MimeType)
        {
            case "text/gemini":
                return ParseGemtextBody(response);

            case "text/plain":
                return ParsePlainTextBody(response);

            case "image/jpeg":
            case "image/png":
                return ParseImageBody(response);

            default:
                throw new ApplicationException($"Unhandled Content Type in Gemini Meta: {response.MimeType}");
        }
    }

    private Encoding GetEncoding(GeminiResponse response)
        => Encoding.GetEncoding((response.Charset == null) ? "utf-8" : response.Charset);

    private ArchivedContent ParseGemtextBody(GeminiResponse response)
    {
        var geminiRoot = DocumentRoot.QuerySelector("div.body div.gemini");
        if (geminiRoot == null)
        {
            throw new ApplicationException("Could not locate Gemini Root inside of HTML!");
        }

        HtmlReverser htmlReverser = new HtmlReverser(WaybackUrl, geminiRoot);
        var gemtextBody = htmlReverser.ReverseToGemtext();

        response.BodyBytes = GetEncoding(response).GetBytes(gemtextBody);

        ArchivedContent ret = new ArchivedContent
        {
            Url = WaybackUrl,
            GeminiResponse = response
        };
        ret.MoreUrls.AddRange(htmlReverser.MoreUrls);

        return ret;
    }

    private ArchivedContent ParsePlainTextBody(GeminiResponse response)
    {
        var preTag = DocumentRoot.QuerySelector("div.body pre");
        if (preTag == null)
        {
            throw new ApplicationException("Could not locate pre tag inside of HTML of text/plain response!");
        }

        string plainText = HttpUtility.HtmlDecode(preTag.TextContent);
        response.BodyBytes = GetEncoding(response).GetBytes(plainText);

        return new ArchivedContent
        {
            Url = WaybackUrl,
            GeminiResponse = response
        };
    }

    private ArchivedContent ParseImageBody(GeminiResponse response)
    {
        var img = DocumentRoot.QuerySelector("div.body img");
        if (img == null)
        {
            throw new ApplicationException("Could not locate pre img inside of HTML of image/* response!");
        }

        string? url = img.GetAttribute("src");

        if (string.IsNullOrEmpty(url))
        {
            throw new ApplicationException("img tag did not have a src attribute, or it was empty");
        }

        if (url.StartsWith("data:"))
        {
            var parts = url.Split(',');
            if (parts.Length != 2)
            {
                throw new ApplicationException("data URL didn't split into 2 parts");
            }
            if (!parts[0].Contains(";base64"))
            {
                throw new ApplicationException("data URL was not using base64 encoding!");
            }
            response.BodyBytes = Convert.FromBase64String(parts[1]);

            return new ArchivedContent
            {
                Url = WaybackUrl,
                GeminiResponse = response
            };
        }

        var fullyQualifiedImgSrc = new Uri(WaybackUrl.Url, url);
        WaybackUrl imgUrl = new WaybackUrl(fullyQualifiedImgSrc);

        var ret = new ArchivedContent
        {
            Url = WaybackUrl
        };
        ret.MoreUrls.Add(imgUrl);

        return ret;
    }

    private IElement ParseToRoot(Uri htmlUrl, string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>();

        IDocument document = context.OpenAsync(req => req.Content(html)).Result;
        return document.DocumentElement;
    }
}