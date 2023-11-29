namespace Kennedy.Data.Utils;

public static class UrlUtility
{
    /// <summary>
    /// Is this is common URL that is used as a proactive request by the crawler?
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static bool IsProactiveUrl(Uri url)
        => IsFaviconUrl(url) || IsRobotsUrl(url) || IsSecurityUrl(url);

    public static bool IsFaviconUrl(Uri url)
        => url.AbsolutePath == "/favicon.txt";

    public static bool IsRobotsUrl(Uri url)
        => url.AbsolutePath == "/robots.txt";

    public static bool IsSecurityUrl(Uri url)
        => url.AbsolutePath == "/.well-known/security.txt";

    /// <summary>
    /// Removes the "kennder-crawler" identify from the query string, if present
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static Uri RemoveCrawlerIdentifier(Uri url)
    {
        if(IsRobotsUrl(url) && url.Query.Length > 0)
        {
            var uriBuilder = new UriBuilder(url);
            uriBuilder.Query = "";
            return uriBuilder.Uri;
        }
        return url;
    }
}

