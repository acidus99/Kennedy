using System;
using System.Linq;
using System.Web;
using Gemini.Net;
using Kennedy.Archive;
using Kennedy.Archive.Db;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Archive;

internal class CachedView : AbstractView
{
    public CachedView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    ArchiveDbContext archive = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");
    SnapshotReader reader = new SnapshotReader(Settings.Global.DataRoot + "Packs/");

    public override void Render()
    {
        var args = ParseArgs();

        if (args.Url == null)
        {
            RenderInvalidUrl();
            return;
        }

        var snapshot = GetSnapshot(args.Url, args.Timestamp);

        if (snapshot == null || snapshot.Url == null)
        {
            RenderNoSnapshot(args.Url, args.Timestamp);
            return;
        }
        else if (snapshot.IsSuccess)
        {
            RenderSuccessSnapshot(snapshot, args.IsRaw);
        }
        else
        {
            RenderOtherStatusSnapshot(snapshot);
        }
    }

    private void RenderSuccessSnapshot(Snapshot snapshot, bool isRawMode)
    {
        if (isRawMode || !snapshot.IsText)
        {
            Response.Write(reader.ReadBytes(snapshot));
            return;
        }

        GeminiResponse response = reader.ReadResponse(snapshot);

        Response.Success();
        Response.Write($"> 💾 Archived View for {FormatUrl(snapshot.Url!.GeminiUrl)} ");
        Response.Write($"captured on {snapshot.Captured.ToString("yyyy-MM-dd")} at ");
        Response.Write($"{snapshot.Captured.ToString("HH:mm:ss")}.");
        if (snapshot.IsGemtext)
        {
            Response.Write(" Gemini links have been rewritten to link to archived content");
        }
        Response.WriteLine();
        Response.WriteLine($"=> {RoutePaths.ViewCached(snapshot, true)} View Raw");
        Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistory(snapshot.Url.GeminiUrl)} More Information");
        var others = GetNextPreviousSnapshots(snapshot);

        if (others.before != null)
        {
            Response.WriteLine($"=> {RoutePaths.ViewCached(others.before)} ⬅️ Previous capture ({others.before.Captured.ToString("yyyy-MM-dd")})");
        }

        if (others.after != null)
        {
            Response.WriteLine($"=> {RoutePaths.ViewCached(others.after)} ➡️ Next capture ({others.after.Captured.ToString("yyyy-MM-dd")})");
        }

        if (!snapshot.IsDuplicate && others.before != null)
        {
            Response.WriteLine($"=> {RoutePaths.ViewDiff(others.before, snapshot)} 🚧 View Differences");
        }

        Response.WriteLine("-=-=-=-=-=-=-");
        Response.WriteLine();

        var text = response.BodyText;

        if (snapshot.IsGemtext)
        {
            GemtextRewriter gemtextRewriter = new GemtextRewriter();
            text = gemtextRewriter.Rewrite(snapshot, text);
            Response.Write(text);
        }
        else
        {
            Response.WriteLine("```");
            Response.WriteLine(text);
            Response.WriteLine("```");
        }
    }

    private void RenderInvalidUrl()
    {
        Response.Success();
        Response.WriteLine("Error Invalid URL. We don't understand that URL format");
        return;
    }

    private void RenderNoSnapshot(GeminiUrl url, DateTime timeStamp)
    {
        Response.Success();
        Response.WriteLine("Sorry, 🏎 Delorean Time Machine couldn't find a snapshot of that URL in its Archive.");
        Response.WriteLine("> " + url);
        Response.WriteLine("Possible reaons:");
        Response.WriteLine("* Link to this URL was a typo in the original source");
        Response.WriteLine("* This URL was excluded from crawling or archiving via robots.txt");
        Response.WriteLine("* You found a bug in Time Machine! Beware world ending paradoxes!");
        Response.WriteLine("Options:");
        Response.WriteLine($"=> {url} Try the URLs directly. It might be live.");
        Response.WriteLine($"=> {RoutePaths.ViewCached(url.RootUrl, timeStamp)} Try looking at the cached version of capsule's home page");
        return;
    }

    private void RenderOtherStatusSnapshot(Snapshot snapshot)
    {
        Response.Success();
        Response.WriteLine($"> This an archived version of {snapshot.Url!.FullUrl} captured on {snapshot.Captured.ToString("yyyy-MM-dd")}. ");
        Response.WriteLine();
        Response.WriteLine("> The server sent the following response for this URL when it was captured:");

        GeminiResponse response = reader.ReadResponse(snapshot);

        Response.WriteLine("```");
        Response.WriteLine($"{response.StatusCode} {response.Meta}");
        Response.WriteLine("```");

        if (response.IsRedirect)
        {
            var redirectUrl = response.GetRedirectUrl();
            if (redirectUrl != null)
            {
                Response.WriteLine($"=> {RoutePaths.ViewCached(redirectUrl, snapshot.Captured)} Follow this Redirect");
            }
        }
    }

    private (GeminiUrl? Url, DateTime Timestamp, bool IsRaw) ParseArgs()
    {
        var args = HttpUtility.ParseQueryString(Request.Url.RawQuery);

        var url = GeminiUrl.MakeUrl(args["url"]);
        var time = ParseTime(args["t"]);
        var isRaw = ((args["raw"]?.ToLower() ?? "") == "true");

        return (url, time, isRaw);
    }

    /// <summary>
    /// Attempts to parse the user's perferred time. If no valid time, use the current time
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    private DateTime ParseTime(string? time)
    {
        if (time != null)
        {
            try
            {
                return new DateTime(Convert.ToInt64(time));
            }
            catch (Exception)
            {
            }
        }
        return DateTime.Now;
    }

    private (Snapshot? before, Snapshot? after) GetNextPreviousSnapshots(Snapshot selected)
    {
        Snapshot? before = null;
        Snapshot? after = null;

        var snaps = archive.Snapshots.Where(x => x.UrlId == selected.Url!.Id)
            .Include(x => x.Url)
            .Where(x => x.Url != null && x.Url.IsPublic)
            .OrderBy(x => x.Captured)
            .ToArray();

        for (int i = 0; i < snaps.Length; i++)
        {
            if (snaps[i].IsDuplicate)
            {
                continue;
            }

            if (snaps[i].Captured < selected.Captured)
            {
                before = snaps[i];
            }
            if (snaps[i].Captured > selected.Captured)
            {
                after = snaps[i];
                break;
            }
        }

        return (before, after);
    }

    /// <summary>
    /// Given a URL, find the snapshot closest to the provided data/time
    /// </summary>
    /// <returns></returns>
    private Snapshot? GetSnapshot(GeminiUrl url, DateTime targetTime)
        => archive.Snapshots
            .Where(x => x.UrlId == url.ID)
            .Include(x => x.Url)
            .Where(x => x.Url != null && x.Url.IsPublic)
            .OrderBy(x => Math.Abs(x.Captured.Ticks - targetTime.Ticks))
            .FirstOrDefault();
}
