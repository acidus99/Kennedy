using Gemini.Net;
using Kennedy.Archive.Db;
using Microsoft.EntityFrameworkCore;
using RocketForce;
using System.Linq;

namespace Kennedy.Server.Views.Archive;

/// <summary>
/// Shows the details about a 
/// </summary>
internal class DiffHistoryView : AbstractView
{
    public DiffHistoryView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        var url = ParseArgs();

        if (url == null)
        {
            Response.Success();
            Response.WriteLine($"# 🏎 DeLorean Time Machine");
            Response.WriteLine();
            Response.WriteLine("Invalid URL. Please enter a fully qualified, valid, Gemini URL.");
            Response.WriteLine("=> /delorean Try Again");
            return;
        }

        ArchiveDbContext db = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");

        var urlEntry = db.Urls
            .Where(x => x.Id == url.ID && x.IsPublic)
            .Include(x => x.Snapshots)
            .FirstOrDefault();

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
        Response.WriteLine($"=> {urlEntry.GeminiUrl.NormalizedUrl} { FormatUrl(urlEntry.GeminiUrl)}");
        Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistory(urlEntry.GeminiUrl.NormalizedUrl)} More Information");

        var snapshots = urlEntry.Snapshots
            .Where(x => !x.IsDuplicate)
            .OrderBy(x => x.Captured)
            .ToArray();

        if (snapshots.Count() < 2)
        {
            Response.WriteLine("Can only view differences if at least 2 unique snapshots of content are available.");
            return;
        }

        var first = snapshots.First();
        var last = snapshots.Last();

        if (!snapshots.Where(x=>x.IsText).Any())
        {
            Response.WriteLine("Can only view differences of Gemtext files");
            return;
        }

        Response.WriteLine("## 🚧 Differences History");

        int currentYear = 0;

        for (int i=1;i<snapshots.Length; i++)
        {
            if (currentYear < snapshots[i].Captured.Year)
            {
                Response.WriteLine($"### {snapshots[i].Captured.Year}");
                currentYear = snapshots[i].Captured.Year;
            }

            RenderDiffLine(snapshots[i - 1], snapshots[i]);
        }
    }

    private void RenderDiffLine(Snapshot previous, Snapshot current)
    {
        Response.Write($"=> {RoutePaths.ViewDiff(previous, current)} ");

        Response.Write($" {previous.Captured.ToString("yyyy-MM-dd")} vs. {current.Captured.ToString("yyyy-MM-dd")} ");
        Response.Write($" • {FormatSize(previous.Size)} vs.{FormatSize(current.Size)}");
        Response.WriteLine();
    }

    private GeminiUrl? ParseArgs()
        => GeminiUrl.MakeUrl(Request.Url.Query);
}
