using System;
using System.Linq;
using System.Web;
using Gemini.Net;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.CrawlData;
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
            {                Response.Success();
                Response.WriteLine($"# 🏎 DeLorean Time Machine");
                Response.WriteLine();
                Response.WriteLine("Invalid URL. Please enter a fully qualified, valid, Gemini URL.");
                Response.WriteLine("=> /delorean Try Again");
                return;
            }

            try
            {
                ArchiveDbContext db = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");

                var urlEntry = db.Urls
                    .Where(x => x.Id == AttemptedUrl.ID)
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
                for (int i=0; i < snapshots.Length; i++)
                {
                    var snapshot = snapshots[i];

                    var contentLabel = "🆕 ";
                    if (i > 0 && snapshots[i - 1].DataHash == snapshot.DataHash)
                    {
                        contentLabel = "";
                    }
                    Response.WriteLine($"=> {RoutePaths.ViewCached(snapshot)} {contentLabel}{snapshot.Captured}. {FormatSize(snapshot.Size)}");
                }
            }
            catch (Exception)
            {
            }
        }

        private void ParseArgs()
        {
            AttemptedUrl = GeminiUrl.MakeUrl(Request.Url.Query);
        }
    }
}
