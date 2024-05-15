using System;
using System.Web;
using Gemini.Net;
using Kennedy.Archive.Db;

namespace Kennedy.Server;

public static class RoutePaths
{
    public const string ImageSearchRoute = "/image-search";
    public const string SearchRoute = "/search";
    public const string SearchStatsRoute = "/stats";

    public const string UrlInfoRoute = "/page-info";

    public const string SiteSearchCreateRoute = "/site-search/create";
    public const string SiteSearchRunRoute = "/site-search/s/";

    public const string ViewCachedRoute = "/archive/cached";
    public const string ViewDiffRoute = "/archive/diff";
    public const string ViewDiffHistoryRoute = "/archive/diff-history";
    public const string ViewUrlUniqueHistoryRoute = "/archive/history";
    public const string ViewUrlFullHistoryRoute = "/archive/history-all";
    public const string SearchArchiveRoute = "/archive/search";
    public const string ArchiveStatsRoute = "/archive/stats";

    public const string CertCheckRoute = "/certs/validator/check";

    public const string SiteHealthRoute = "/reports/site-health";
    public const string DomainBacklinksRoute = "/reports/domain-backlinks";

    public static string ImageSearch(string query)
        => $"{ImageSearchRoute}?{HttpUtility.UrlEncode(query)}";

    public static string Search(string query)
        => $"{SearchRoute}?{HttpUtility.UrlEncode(query)}";

    public static string SiteSearch(string capsule)
        => $"gemini://kennedy.gemi.dev{SiteSearchRunRoute}{capsule}/";

    public static string ViewCached(GeminiUrl url)
        => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}&t={DateTime.Now}";

    public static string ViewCached(string url, DateTime snapshotTime)
        => ViewCached(new GeminiUrl(url), snapshotTime);

    public static string ViewCached(Snapshot snapshot, bool useRaw = false)
    {
        if (snapshot.Url == null)
        {
            throw new ArgumentNullException(nameof(snapshot), "Snapshot URL cannot be null.");
        }
        return ViewCached(snapshot.Url.GeminiUrl, snapshot.Captured, useRaw);
    }

    public static string ViewCached(GeminiUrl url, DateTime snapshotTime, bool useRaw = false)
        => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}&t={snapshotTime.Ticks}&raw={useRaw}";

    public static string ViewDiffHistory(GeminiUrl url)
        => $"{ViewDiffHistoryRoute}?{HttpUtility.UrlEncode(url.NormalizedUrl)}";

    public static string ViewDiff(Snapshot previous, Snapshot current, bool showFull = false)
        => $"{ViewDiffRoute}?url={HttpUtility.UrlEncode(current.Url!.GeminiUrl.NormalizedUrl)}&pt={previous.Captured.Ticks}&t={current.Captured.Ticks}&full={showFull}";

    public static string ViewMostRecentCached(GeminiUrl url)
        => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}";

    public static string ViewUrlInfo(GeminiUrl url)
        => $"{UrlInfoRoute}?{HttpUtility.UrlEncode(url.NormalizedUrl)}";

    public static string ViewUrlUniqueHistory(GeminiUrl url)
        => ViewUrlUniqueHistory(url.NormalizedUrl);

    public static string ViewUrlUniqueHistory(string url)
        => $"{ViewUrlUniqueHistoryRoute}?{HttpUtility.UrlEncode(url)}";

    public static string ViewUrlFullHistory(GeminiUrl url)
      => ViewUrlFullHistory(url.NormalizedUrl);

    public static string ViewUrlFullHistory(string url)
        => $"{ViewUrlFullHistoryRoute}?{HttpUtility.UrlEncode(url)}";
}