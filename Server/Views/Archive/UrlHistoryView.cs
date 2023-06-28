using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Gemini.Net;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.SearchIndex;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Archive
{

    /// <summary>
    /// Shows the details about a 
    /// </summary>
    internal class UrlHistoryView :AbstractView
    {

        GeminiUrl? AttemptedUrl;

        public UrlHistoryView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            ParseArgs();

            if(AttemptedUrl == null)
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
                .Where(x => x.Id == AttemptedUrl.ID && x.IsPublic)
                .Include(x => x.Snapshots).
                FirstOrDefault();

            if (urlEntry == null)
            {
                Response.Success();
                Response.WriteLine($"# 🏎 DeLorean Time Machine");
                Response.WriteLine("No snapshots for that URL");
                Response.WriteLine();
                Response.WriteLine($"=> {RoutePaths.ViewUrlHistoryRoute} Search Time Machine for cached Content");
                Response.WriteLine("=> /search 🔭 New Kennedy Search");
                return;
            }

            Response.Success();
            Response.WriteLine($"# 🏎 DeLorean Time Machine");
            Response.WriteLine();
            Response.WriteLine("Found this URL in Time Machine!");
            Response.WriteLine($"=> {urlEntry.GeminiUrl.NormalizedUrl} {urlEntry.GeminiUrl.NormalizedUrl}");

            var snapshots = urlEntry.Snapshots.OrderBy(x => x.Captured).ToArray();

            var first = snapshots.First();
            var last = snapshots.Last();

            Response.WriteLine($"Saved {snapshots.Length} times between {first.Captured.ToString("MMMM d yyyy")} and {last.Captured.ToString("MMMM d yyyy")}");
            Response.WriteLine($"Unique Saves: {snapshots.GroupBy(x=>x.DataHash).Count()}");

            Response.WriteLine("## Saved copies");

            int currentYear = 0;

            foreach(var snapshot in snapshots)
            {
                if(currentYear < snapshot.Captured.Year)
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
                Response.Write($"{FormatSize(snapshot?.Size ?? 0)}");
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
}
