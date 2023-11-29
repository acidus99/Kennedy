using System;
using Gemini.Net;
using System.Web;

using Kennedy.Archive.Db;

namespace Kennedy.Server
{
	public static class RoutePaths 
	{
        public const string ImageSearchRoute = "/image-search";
        public const string SearchRoute = "/search";
        public const string SearchStatsRoute = "/stats";
        public const string SiteSearchRoute = "/sitesearch";
        public const string UrlInfoRoute = "/page-info";

        public const string ViewCachedRoute = "/archive/cached";
        public const string ViewUrlHistoryRoute = "/archive/history";
        public const string SearchArchiveRoute = "/archive/search";
        public const string ArchiveStatsRoute = "/archive/stats";

        public const string SiteHealthRoute = "/reports/site-health";
        public const string DomainBacklinksRoute = "/reports/domain-backlinks";

        public static string ImageSearch(string query)
            => $"{ImageSearchRoute}?{HttpUtility.UrlEncode(query)}";

        public static string Search(string query)
            => $"{SearchRoute}?{HttpUtility.UrlEncode(query)}";

        public static string SiteSearch(string capsule)
            => $"gemini://kennedy.gemi.dev{SiteSearchRoute}/{capsule}/";

        public static string ViewCached(GeminiUrl url)
            => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}&t={DateTime.Now}";

        public static string ViewCached(string url, DateTime snapshotTime)
            => ViewCached(new GeminiUrl(url), snapshotTime);

        public static string ViewCached(Snapshot snapshot, bool useRaw = false)
        {
            if(snapshot.Url == null)
            {
                throw new ArgumentNullException(nameof(snapshot), "Snapshot URL cannot be null.");
            }
            return ViewCached(snapshot.Url.GeminiUrl, snapshot.Captured, useRaw);
        }

        public static string ViewCached(GeminiUrl url, DateTime snapshotTime, bool useRaw = false)
            => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}&t={snapshotTime.Ticks}&raw={useRaw}";

        public static string ViewMostRecentCached(GeminiUrl url)
            => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}";

        public static string ViewUrlInfo(GeminiUrl url)
            => $"{UrlInfoRoute}?{HttpUtility.UrlEncode(url.NormalizedUrl)}";

        public static string ViewUrlHistory(GeminiUrl url)
            => ViewUrlHistory(url.NormalizedUrl);

        public static string ViewUrlHistory(string url)
            => $"{ViewUrlHistoryRoute}?{HttpUtility.UrlEncode(url)}";


    }
}

