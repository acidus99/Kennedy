using System;
using Gemini.Net;
using System.Web;

using Kennedy.Archive.Db;
using System.Runtime.Intrinsics.X86;

namespace Kennedy.Server
{
	public static class RoutePaths
	{

        public const string ViewCachedRoute = "/archive/cached";

        public const string ViewUrlHistoryRoute = "/archive/history";

        public static string ViewCached(GeminiUrl url)
            => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}&t={DateTime.Now}";

        public static string ViewCached(string url, DateTime snapshotTime)
            => ViewCached(new GeminiUrl(url), snapshotTime);

        public static string ViewCached(Snapshot snapshot, bool useRaw = false)
            => ViewCached(snapshot.Url.GeminiUrl, snapshot.Captured, useRaw);

        public static string ViewCached(GeminiUrl url, DateTime snapshotTime, bool useRaw = false)
            => $"{ViewCachedRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}&t={snapshotTime.Ticks}&raw={useRaw}";

        public static string ViewUrlHistory(GeminiUrl url)
            => $"{ViewUrlHistoryRoute}?url={HttpUtility.UrlEncode(url.NormalizedUrl)}";

    }
}

