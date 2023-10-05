namespace Kennedy.WarcConverters.MozzPortalImport;

using System;
using System.Web;
using System.Security.Cryptography.X509Certificates;

using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Gemini.Net;
using System.Reflection.Metadata;

public class MozzHtmlConverter
{
    string Html;

    WaybackUrl WaybackUrl;

    IElement DocumentRoot;

    GeminiResponse response = null!;

    GeminiUrl ProxiedUrl;

    public MozzHtmlConverter(WaybackUrl waybackUrl, string html)
    {
        if(!waybackUrl.IsMozzUrl)
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
        if(index < 0)
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
        if(responseLine == null)
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
            var bodyBytes = ParseBody(response);
            response.BodyBytes = bodyBytes;
            if(bodyBytes == null)
            {
                response.IsBodyTruncated = true;
            }
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
        if(table == null)
        {
            throw new ApplicationException($"Could not find a table in the parsed HTML.");
        }

        var cells = table.QuerySelectorAll("td").ToArray();
        if (cells.Length != 4)
        {
            throw new ApplicationException($"Did not find 4 cells in response table! Found {cells.Length}");
        }

        var statusCode = cells[1].TextContent.Trim();

        if(string.IsNullOrEmpty(statusCode))
        {
            return null;
        }

        statusCode = statusCode.Substring(0, 2);
        var meta = cells[3].TextContent.Trim();
        return $"{statusCode} {meta}";
    }

    private byte[]? ParseBody(GeminiResponse response)
    {
        switch(response.MimeType)
        {
            case "text/gemini":
                return ParseGemtext(response);

            case "text/plain":
                return ParsePlainText(response);

            case "image/jpeg":
            case "image/png":
                return ParseImage(response);

            default:
                throw new ApplicationException($"Unhandled Content Type in Gemini Meta: {response.MimeType}");
        }
    }

    private Encoding GetEncoding(GeminiResponse response)
        => Encoding.GetEncoding((response.Charset == null) ? "utf-8" : response.Charset);

    private byte[] ParseGemtext(GeminiResponse response)
    {
        var geminiRoot = DocumentRoot.QuerySelector("div.body div.gemini");
        if(geminiRoot == null)
        {
            throw new ApplicationException("Could not locate Gemini Root inside of HTML!");
        }

        HtmlReverser htmlReverser = new HtmlReverser(WaybackUrl, geminiRoot);
        var gemtextBody = htmlReverser.ReverseToGemtext();

        return GetEncoding(response).GetBytes(gemtextBody);
    }

    private byte[] ParsePlainText(GeminiResponse response)
    {
        var preTag = DocumentRoot.QuerySelector("div.body pre");
        if (preTag == null)
        {
            throw new ApplicationException("Could not locate pre tag inside of HTML of text/plain response!");
        }

        string plainText = HttpUtility.HtmlDecode(preTag.TextContent);
        return GetEncoding(response).GetBytes(plainText);
    }

    private byte[]? ParseImage(GeminiResponse response)
    {
        var img = DocumentRoot.QuerySelector("div.body img");
        if (img == null)
        {
            throw new ApplicationException("Could not locate pre img inside of HTML of image/* response!");
        }

        string? url = img.GetAttribute("src");

        if(string.IsNullOrEmpty(url))
        {
            throw new ApplicationException("img tag did not have a src attribute, or it was empty");
        }

        if(url.StartsWith("data:") )
        {
            var parts = url.Split(',');
            if(parts.Length !=2)
            {
                throw new ApplicationException("data URL didn't split into 2 parts");
            }
            if (!parts[0].Contains(";base64"))
            {
                throw new ApplicationException("data URL was not using base64 encoding!");
            }
            return Convert.FromBase64String(parts[1]);
        }

        Uri imgUrl = (new Uri(WaybackUrl.Url, url));

        HttpRequestor httpRequestor = new HttpRequestor();
        var imageResponse = httpRequestor.SendRequest(imgUrl);
        if(imageResponse.IsSuccessStatusCode)
        {
            return imageResponse.Content.ReadAsByteArrayAsync().Result;
        }
        return null;
    }

    private IElement ParseToRoot(Uri htmlUrl, string html)
    {
        string hackyHtml = hack();
        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>();

        IDocument document = context.OpenAsync(req => req.Content(html)).Result;

        return document.DocumentElement;
    }

    private string hack()
    {
        var config = Configuration.Default.WithDefaultLoader();
        var address = "https://web.archive.org/web/20220606030738if_/https://portal.mozz.us/gemini/arcanesciences.com/gemlog/22-06-05/words.txt";
        var context = BrowsingContext.New(config);
        var document = context.OpenAsync(address).Result;
        return document.DocumentElement.OuterHtml;
    }

    
}

