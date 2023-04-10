using System;
using System.Linq;
using System.IO;
using System.Web;

using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.SearchIndex;
using Kennedy.SearchIndex.Models;
using RocketForce;
using Kennedy.Archive.Db;
using Kennedy.Archive;

namespace Kennedy.Server.Views.Archive
{
    internal class CachedView :AbstractView
    {
        public CachedView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        Snapshot Snapshot;

        GeminiUrl? AttemptedUrl;
        DateTime AttemptedTime = DateTime.Now;
        bool RawMode = false;

        ArchiveDbContext archive = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");

        public override void Render()
        {
            ParseArgs();

            if (Snapshot == null)
            {
                Response.Success();
                Response.WriteLine("Sorry, 🏎 Delorean Time Machine couldn't find a snapshot of that URL in its Archive.");
                if (AttemptedUrl != null)
                {
                    Response.WriteLine("> " + AttemptedUrl);
                }
                Response.WriteLine("Possible reaons:");
                Response.WriteLine("* Link to this URL was a typo in the original source");
                Response.WriteLine("* This URL was excluded from crawling or archiving via robots.txt");
                Response.WriteLine("* You found a bug in Time Machine! Beware world ending paradoxes!");
                Response.WriteLine("Options:");
                Response.WriteLine($"=> {RoutePaths.ViewCached(AttemptedUrl.RootUrl, AttemptedTime)} Try looking at the cached version of capsule's home page");
                return;
            }

            SnapshotReader reader = new SnapshotReader(Settings.Global.DataRoot + "Packs/");


            if (RawMode || Snapshot.ContentType != Data.ContentType.Text)
            {
                Response.Success(Snapshot.Meta); 
                Response.Write(reader.ReadBytes(Snapshot));
                return;
            }


            var text = reader.ReadText(Snapshot);
            Response.Success();
            Response.Write($"> This an archived version of {Snapshot.Url.FullUrl} as seen by the Kennedy Crawler on {Snapshot.Captured.ToString("yyyy-MM-dd")}. ");
            if (Snapshot.IsGemtext)
            {
                Response.Write("Gemini links have been rewritten to link to archived content");


            }
            Response.WriteLine();
            Response.WriteLine($"=> {RoutePaths.ViewUrlHistory(Snapshot.Url.GeminiUrl)} More Information in 🏎 Delorean Time Machine");
            Response.WriteLine($"=> {RoutePaths.ViewCached(Snapshot, true)} View Raw");
            Response.WriteLine();

            if (Snapshot.IsGemtext)
            {
                GemtextRewriter gemtextRewriter = new GemtextRewriter();
                text = gemtextRewriter.Rewrite(Snapshot, text);
                Response.Write(text);
            }
            else
            {
                Response.WriteLine("```");
                Response.WriteLine(text);
                Response.WriteLine("```");
            }            
        }

        private void ParseArgs()
        {
            var args = HttpUtility.ParseQueryString(Request.Url.RawQuery);
            if ((args["raw"]?.ToLower() ?? "") == "true")
            {
                RawMode = true;
            }

            var snapID = args["sid"] ?? "";
            if (snapID != "")
            {
                Snapshot = GetSnapshot(Convert.ToInt64(snapID));
                return;
            }

            AttemptedUrl = GeminiUrl.MakeUrl(args["url"]);

            if (AttemptedUrl!= null)
            {
                AttemptedTime = new DateTime(Convert.ToInt64(args["t"]));
                Snapshot = GetSnapshot(AttemptedUrl.ID, AttemptedTime);
            }
        }

        /// <summary>
        /// given a snapshot id, get the snapshot
        /// </summary>
        /// <param name="snapID"></param>
        /// <returns></returns>
        private Snapshot GetSnapshot(long snapID)
            => archive.Snapshots.Where(x => x.Id == snapID).Include(x => x.Url).FirstOrDefault();

        /// <summary>
        /// Given a URL ID, find the snapshot closest to the provided data/time
        /// </summary>
        /// <returns></returns>
        private Snapshot GetSnapshot(long urlID, DateTime targetTime)
            => archive.Snapshots
                .Where(x => x.UrlId == urlID)
                .Include(x => x.Url)
                .OrderBy(x => Math.Abs(x.Captured.Ticks - targetTime.Ticks))
                .FirstOrDefault();

    }
}
