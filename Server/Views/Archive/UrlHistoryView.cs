using Gemini.Net;
using Kennedy.Archive.Db;
using Microsoft.EntityFrameworkCore;
using RocketForce;
using System.Linq;

namespace Kennedy.Server.Views.Archive;

/// <summary>
/// Shows the details about a 
/// </summary>
internal class UrlHistoryView : AbstractView
{
    public bool ShowAllSnapshots { get; set; } = false;

    GeminiUrl? AttemptedUrl;

    public UrlHistoryView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        ParseArgs();

        if (AttemptedUrl == null)
        {
            Response.Success();
            Response.WriteLine($"# 🏎 DeLorean Time Machine");
            Response.WriteLine();
            Response.WriteLine("Invalid URL. Please enter a fully qualified, valid, Gemini URL.");
            Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistoryRoute} Try another URL");
            Response.WriteLine($"=> {RoutePaths.SearchArchiveRoute} Search for parts of a URL");
            return;
        }

        ArchiveDbContext db = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");

        var urlEntry = db.Urls
            .Where(x => x.Id == AttemptedUrl.ID && x.IsPublic)
            .Include(x => x.Snapshots).
            FirstOrDefault();

        if (urlEntry == null)
        {
            Response.Success();
            Response.WriteLine($"# 🏎 DeLorean Time Machine");
            Response.WriteLine("No snapshots for that URL");
            Response.WriteLine();
            Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistoryRoute} Search Time Machine for cached Content");
            Response.WriteLine("=> /search 🔭 New Kennedy Search");
            return;
        }

        Response.Success();
        Response.WriteLine($"# 🏎 DeLorean Time Machine");
        Response.WriteLine();
        Response.WriteLine($"=> {urlEntry.GeminiUrl.NormalizedUrl} {FormatUrl(urlEntry.GeminiUrl)}");

        var snapshots = urlEntry.Snapshots.OrderBy(x => x.Captured).ToArray();

        var first = snapshots.First();
        var last = snapshots.Last();

        Response.WriteLine($"Saved {snapshots.Length} times between {first.Captured.ToString("MMMM d yyyy")} and {last.Captured.ToString("MMMM d yyyy")}");

        var uniqueCount = snapshots.GroupBy(x => x.DataHash).Count();
        Response.WriteLine($"Unique snapshots: {uniqueCount}");

        var truncatedCount = snapshots.Where(x => x.IsBodyTruncated).Count();
        if (truncatedCount > 0)
        {
            Response.WriteLine();
            Response.WriteLine($"{truncatedCount} snapshots are truncated, meaning the entire file is not there. Depending on the type file type, these truncated snapshots may not be able to be opened.");
        }

        if (ShowAllSnapshots)
        {
            Response.WriteLine("## All Snapshots");
            Response.WriteLine($"Showing all {snapshots.Length} snapshots for this URL.");
            Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistory(urlEntry.GeminiUrl)} Show only unique snapshots");
        }
        else
        {
            Response.WriteLine("## Unique Snapshots");
            Response.WriteLine($"Showing {uniqueCount} snapshots that have unique content for this URL.");
            Response.WriteLine($"=> {RoutePaths.ViewUrlFullHistory(urlEntry.GeminiUrl)} Show all snapshots");

            snapshots = snapshots.Where(x => !x.IsDuplicate).ToArray();
        }

        if (uniqueCount > 1)
        {
            Response.WriteLine($"=> {RoutePaths.ViewDiffHistory(AttemptedUrl)} 🚧 View Snapshot Differences");
        }

        int currentYear = 0;

        foreach (var snapshot in snapshots)
        {
            if (currentYear < snapshot.Captured.Year)
            {
                Response.WriteLine($"### {snapshot.Captured.Year}");
                currentYear = snapshot.Captured.Year;
            }
            RenderSnapshot(snapshot);
        }
    }

    private void RenderSnapshot(Snapshot snapshot)
    {
        Response.Write($"=> {RoutePaths.ViewCached(snapshot)} ");
        if (!snapshot.IsDuplicate)
        {
            Response.Write("🆕 ");
        }
        Response.Write(snapshot.Captured.ToString("yyyy-MM-dd"));
        Response.Write($" • ");
        if (GeminiParser.IsSuccessStatus(snapshot.StatusCode))
        {
            Response.Write($"{snapshot.Mimetype} • ");
            Response.Write($"{FormatSize(snapshot.Size)}");
            if (snapshot.IsBodyTruncated)
            {
                Response.Write(" • Truncated Body");
            }
        }
        else if (GeminiParser.IsRedirectStatus(snapshot.StatusCode))
        {
            Response.Write($"↩️ Redirect");
        }
        else
        {
            Response.Write($"Status Code: {snapshot.StatusCode}");
        }
        Response.WriteLine();
    }

    private void ParseArgs()
    {
        AttemptedUrl = GeminiUrl.MakeUrl(Request.Url.Query);
    }
}
