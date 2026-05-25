using System;
using System.Linq;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Search;

internal class SearchStatsView : AbstractView
{
    private static readonly TimeSpan StatsCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly object StatsLock = new();
    private static CachedStats? _cached;

    public SearchStatsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        Response.Success();
        Response.WriteLine("# 📏 Kennedy Stats");
        Response.WriteLine();

        var stats = GetStats();

        Response.WriteLine($"Active Capsules: {FormatCount(stats.Domains)}");
        Response.WriteLine($"Total Urls: {FormatCount(stats.TotalUrls)}");
        Response.WriteLine($"Documents: {FormatCount(stats.Documents)}");
        Response.WriteLine($"Last Updated: {stats.LastUpdated}");
    }

    private static CachedStats GetStats()
    {
        var now = DateTime.UtcNow;

        lock (StatsLock)
        {
            if (_cached != null && now < _cached.ExpiresUtc)
            {
                return _cached;
            }

            var options = new DbContextOptionsBuilder<KennedyDbContext>()
                .UseSqlite($"Data Source={Settings.Global.SearchDbFile}")
                .Options;

            using var db = new KennedyDbContext(options);

            var domains = db.UrlRegistry
                .Where(x => x.LastStatusCode != Gemini.Net.GeminiParser.ConnectionErrorStatusCode)
                .Select(x => new { x.Scheme, x.Host, x.Port })
                .Distinct()
                .LongCount();

            var totalUrls = db.UrlRegistry.LongCount();
            var documents = db.Documents.LongCount();
            var lastUpdated = db.UrlRegistry
                .Where(x => x.LastVisit != null)
                .Select(x => x.LastVisit)
                .Max();

            _cached = new CachedStats
            {
                Domains = domains,
                TotalUrls = totalUrls,
                Documents = documents,
                LastUpdated = lastUpdated,
                ExpiresUtc = now + StatsCacheTtl
            };

            return _cached;
        }
    }

    private sealed class CachedStats
    {
        public long Domains { get; init; }
        public long TotalUrls { get; init; }
        public long Documents { get; init; }
        public DateTime? LastUpdated { get; init; }
        public DateTime ExpiresUtc { get; init; }
    }
}
