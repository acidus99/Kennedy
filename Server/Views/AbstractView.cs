using System;
using System.Web;
using Gemini.Net;
using RocketForce;

namespace Kennedy.Server.Views;

internal abstract class AbstractView
{
    protected GeminiRequest Request;
    protected Response Response;
    protected GeminiServer App;

    public AbstractView(GeminiRequest request, Response response, GeminiServer app)
    {
        Request = request;
        Response = response;
        App = app;
    }

    public abstract void Render();

    //removes whitepsace so a user query cannot inject new gemtext lines into the output
    protected string SanitizedQuery
        => Request.Url.Query.Replace("\r", "").Replace("\n", "").Trim();

    protected string FormatCount(int i)
        => i.ToString("N0");

    protected string FormatCount(long i)
        => i.ToString("N0");

    protected string FormatDomain(string domain, string? favicon)
        => (favicon != null) ? $"{favicon} {domain}" : $"{domain}";

    protected string FormatSize(int bodySize)
        => FormatSize(Convert.ToInt64(bodySize));

    protected string FormatSize(long bodySize)
    {
        const decimal kb = 1024m;
        const decimal mb = kb * 1024m;
        const decimal gb = mb * 1024m;

        var absoluteSize = Math.Abs((decimal)bodySize);

        if (absoluteSize >= gb)
        {
            return $"{FormatSizeValue(bodySize / gb)} GB";
        }

        if (absoluteSize >= mb)
        {
            return $"{FormatSizeValue(bodySize / mb)} MB";
        }

        if (absoluteSize >= kb)
        {
            return $"{FormatSizeValue(bodySize / kb)} KB";
        }

        return $"{FormatCount(bodySize)} bytes";
    }

    private string FormatSizeValue(decimal size)
        => decimal.Truncate(size) == size ?
            size.ToString("N0") :
            size.ToString("N2");

    protected string FormatUrl(GeminiUrl url, string? favicon = null)
    {
        var parts = (url.Hostname + url.Path).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var ret = string.Join(" › ", parts);
        if (ret.Length > 80)
        {
            ret = ret.Substring(0, 80) + '…';
        }
        return favicon == null ? ret : $"{favicon} {ret}";
    }

    protected string FormatFilename(GeminiUrl url)
        => (url.Filename.Length > 0) ?
            HttpUtility.UrlDecode(url.Filename) :
            "/";

    protected string FormatLanguage(string twoLetterISOLanguageName)
    {
        var culture = new System.Globalization.CultureInfo(twoLetterISOLanguageName);
        return culture.DisplayName;
    }
}
