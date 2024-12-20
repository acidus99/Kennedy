using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Gemini.Net;
using Kennedy.Archive;
using Kennedy.Archive.Db;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Archive;

internal class DiffView : AbstractView
{
    private class DiffPair
    {
        public Snapshot? Previous;
        public Snapshot? Current;
    }

    private bool ShowFullHistory = false;
    private DiffPair PreviousDiff = new DiffPair();
    private DiffPair CurrentDiff = new DiffPair();
    private DiffPair NextDiff = new DiffPair();

    public DiffView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }


    ArchiveDbContext archive = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");
    SnapshotReader reader = new SnapshotReader(Settings.Global.DataRoot + "Packs/");

    public override void Render()
    {
        var args = ParseArgs();

        if (args.url == null || args.current == null || args.previous == null)
        {
            RenderInvalidDiff();
            return;
        }
        ShowFullHistory = args.showFull;
        LoadSnapshots(args.url, args.previous.Value, args.current.Value);

        RenderHeader();
        RenderDiff();
    }

    private void RenderDiff()
    {
        if (CurrentDiff.Current == null || CurrentDiff.Previous == null)
        {
            RenderInvalidDiff();
            return;
        }

        var prevResponse = reader.ReadResponse(CurrentDiff.Previous);
        var currResponse = reader.ReadResponse(CurrentDiff.Current);

        if (!prevResponse.IsSuccess || !currResponse.IsSuccess)
        {
            Response.WriteLine("Status codes differ between snapshots. Cannot show a Differences view.");
            RenderResponseLine(CurrentDiff.Previous, prevResponse);
            RenderResponseLine(CurrentDiff.Current, currResponse);
            return;
        }

        if (!CurrentDiff.Current.IsText || !CurrentDiff.Previous.IsText)
        {
            Response.WriteLine("Non-text responses selected. Differences view can only by used to show text responses.");
            RenderResponseLine(CurrentDiff.Previous, prevResponse);
            RenderResponseLine(CurrentDiff.Current, currResponse);
            return;
        }

        var diff = InlineDiffBuilder.Diff(prevResponse.BodyText, currResponse.BodyText);

        RenderLineDiffs(diff.Lines, ShowFullHistory);
    }

    private void RenderResponseLine(Snapshot snapshot, GeminiResponse response)
    {
        Response.WriteLine($"Response line on {snapshot.Captured.ToString("yyyy-MM-dd HH:mm:ss")}");
        Response.WriteLine("```");
        Response.WriteLine(response.ResponseLine);
        Response.WriteLine("```");
    }

    private void RenderHeader()
    {
        Response.Success();
        Response.WriteLine($"> 🚧 Differences View for {FormatUrl(CurrentDiff.Current!.Url!.GeminiUrl)}");
        Response.WriteLine($"* Between {CurrentDiff.Previous!.Captured.ToString("yyyy-MM-dd HH:mm:ss")} GMT and {CurrentDiff.Current.Captured.ToString("yyyy-MM-dd HH:mm:ss")} GMT");
        Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistory(CurrentDiff.Current.Url.GeminiUrl)} More Information");

        if (ShowFullHistory)
        {
            Response.WriteLine($"=> {RoutePaths.ViewDiff(CurrentDiff.Previous, CurrentDiff.Current, false)} Showing all lines. See only changed lines?");
        }
        else
        {
            Response.WriteLine($"=> {RoutePaths.ViewDiff(CurrentDiff.Previous, CurrentDiff.Current, true)} Showing changed lines. See all lines?");
        }


        if (PreviousDiff.Previous != null && PreviousDiff.Current != null)
        {
            Response.Write($"=> {RoutePaths.ViewDiff(PreviousDiff.Previous, PreviousDiff.Current, ShowFullHistory)} ");
            Response.WriteLine($"⬅️ 🚧 Previous difference ({PreviousDiff.Previous.Captured.ToString("yyyy-MM-dd HH:mm:ss")} GMT to {PreviousDiff.Current.Captured.ToString("yyyy-MM-dd HH:mm:ss")} GMT)");
        }

        if (NextDiff.Previous != null && NextDiff.Current != null)
        {
            Response.Write($"=> {RoutePaths.ViewDiff(NextDiff.Previous, NextDiff.Current, ShowFullHistory)} ");
            Response.WriteLine($"➡️ 🚧 Next difference ({NextDiff.Previous.Captured.ToString("yyyy-MM-dd HH:mm:ss")} GMT to {NextDiff.Current.Captured.ToString("yyyy-MM-dd HH:mm:ss")} GMT)");
        }

        Response.WriteLine("-=-=-=-=-=-=-");
        Response.WriteLine();
    }

    private void RenderLineDiffs(List<DiffPiece> lines, bool renderUnchanged = false)
    {
        Response.WriteLine("``` diff view");

        foreach (var line in lines)
        {
            if (!renderUnchanged && line.Type == ChangeType.Unchanged)
            {
                continue;
            }

            if (line.Position.HasValue)
            {
                Response.Write(line.Position.Value.ToString());
            }
            Response.Write("\t");
            switch (line.Type)
            {
                case ChangeType.Inserted:
                    Response.Write("+ ");
                    break;
                case ChangeType.Deleted:
                    Response.Write("- ");
                    break;
                case ChangeType.Modified:
                    Response.Write("M ");
                    break;
                default:
                    Response.Write("  ");
                    break;
            }

            Response.WriteLine(line.Text);
        }
        Response.WriteLine("```");
    }

    private void RenderInvalidDiff()
    {
        Response.Success();
        Response.WriteLine("Error Invalid Snapshots. We cannot do a diff.");
        return;
    }

    private (GeminiUrl? url, DateTime? previous, DateTime? current, bool showFull) ParseArgs()
    {
        var args = HttpUtility.ParseQueryString(Request.Url.RawQuery);
        //The NameValueCollection isn't like a Dictionary.
        //getting a key that doesn't exist returns null instead of throwing an exception
        //Worse.. Convert.* methods will return a default if given a null so you get 0 used
        //to create the DateTime. Check for explicit nulls instead

        GeminiUrl? url = GeminiUrl.MakeUrl(args["url"]!);
        DateTime? previous = (args["t"] != null) ? new DateTime(Convert.ToInt64(args["pt"]!)) : null;
        DateTime? current = (args["t"] != null) ? new DateTime(Convert.ToInt64(args["t"]!)) : null;
        bool showFull = (args["full"] != null) && Convert.ToBoolean(args["full"]);

        return (url, previous, current, showFull);
    }

    private void LoadSnapshots(GeminiUrl url, DateTime previous, DateTime current)
    {
        var snapshots = archive.Snapshots
            .Where(x => x.UrlId == url.ID)
            .Include(x => x.Url)
            .Where(x => x.Url != null && x.Url.IsPublic && !x.IsDuplicate)
            .OrderBy(x => x.Captured).ToArray();

        for (int i = 0; i < snapshots.Length; i++)
        {
            //did we find the snapshots that aligns with our "previous"?
            if (snapshots[i].Captured == previous)
            {
                CurrentDiff.Previous = snapshots[i];
                if (i > 0)
                {
                    PreviousDiff.Previous = snapshots[i - 1];
                    PreviousDiff.Current = snapshots[i];
                }
            }
            else if (snapshots[i].Captured == current)
            {
                CurrentDiff.Current = snapshots[i];
                if (i + 1 < snapshots.Length)
                {
                    NextDiff.Previous = snapshots[i];
                    NextDiff.Current = snapshots[i + 1];
                }
                break;
            }
        }
    }
}
