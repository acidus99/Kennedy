using System.Text.RegularExpressions;
using System.Web;
using Gemini.Net;

namespace Kennedy.WarcConverters.MozzPortalImport;

/// <summary>
/// represents a snapshot of content from the internet archive, for URLs using the Mozz proxy
/// </summary>
public class WaybackUrl
{
    static readonly Regex CapturedTimeRegex = new Regex(@"\/web\/(\d{14})", RegexOptions.Compiled);

    /// <summary>
    /// When was this content captured?
    /// </summary>
    public DateTime Captured { get; private set; }

    /// <summary>
    /// Wayback machine URL
    /// </summary>
    public Uri Url { get; private set; }

    /// <summary>
    /// URL of resoure saved in 
    /// </summary>
    public Uri SourceUrl { get; private set; }

    public WaybackUrl(string url)
        : this(new Uri(url))
    { }

    public WaybackUrl(Uri url)
    {
        Url = url;

        var match = CapturedTimeRegex.Match(Url.AbsolutePath);
        if (!match.Success)
        {
            throw new ArgumentException("Could not extract capture time from URL.", nameof(url));
        }
        Captured = DateTime.ParseExact(match.Groups[1].Value, "yyyyMMddHHmmss", null);

        //skip us into the path, into the segement with the timestamp
        var path = url.PathAndQuery.Substring(5);


        var index = path.IndexOf('/');
        if (index < 0)
        {
            throw new ArgumentException("Could not parse out source URL from wayback url. Could not find slash.", nameof(url));
        }

        if (index + 1 > path.Length)
        {
            throw new ArgumentException("Could not parse out source URL from wayback url. String too short.", nameof(url));
        }

        SourceUrl = new Uri(path.Substring(index + 1));
    }

    /// <summary>
    /// Is this a URL that is using the Mozz proxy?
    /// Use hostname and path to Gemini proxy output to filter
    /// </summary>
    public bool IsMozzUrl
        => (SourceUrl.Host == "portal.mozz.us" && SourceUrl.AbsolutePath.StartsWith("/gemini/"));

    public bool IsCertificateRequest
        => SourceUrl.Query.StartsWith("?crt=");

    public bool IsRawRequest
        => SourceUrl.Query.StartsWith("?raw=");

    public GeminiUrl GetProxiedUrl()
    {
        if (!IsMozzUrl)
        {
            throw new ApplicationException("Attempting to get the Gemini URL for a wayback URL that does not point at mozz proxy.");
        }

        string bareUrl = HttpUtility.UrlDecode(SourceUrl.AbsolutePath.Substring("/gemini/".Length));
        return new GeminiUrl("gemini://" + bareUrl);
    }

    public static string GetRawSourceUrl(Uri url)
    {
        //skip us into the path, into the segement with the timestamp
        var path = url.PathAndQuery.Substring(5);

        var index = path.IndexOf('/');
        if (index < 0)
        {
            throw new ArgumentException("Could not parse out source URL from wayback url. Could not find slash.", nameof(url));
        }

        if (index + 1 > path.Length)
        {
            throw new ArgumentException("Could not parse out source URL from wayback url. String too short.", nameof(url));
        }

        return path.Substring(index + 1);
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        var item = obj as WaybackUrl;

        if (item == null)
        {
            return false;
        }

        return Url.Equals(item.Url);
    }

    public override int GetHashCode()
        => Url.GetHashCode();
}