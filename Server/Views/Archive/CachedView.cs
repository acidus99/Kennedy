using System;
using System.Linq;
using System.IO;
using System.Web;

using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
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
                Response.WriteLine("Sorry, Delorean Time Machine couldn't find a snapshot of that URL in its Archive.");
                if (AttemptedUrl != null)
                {
                    Response.WriteLine("> " + AttemptedUrl);
                }
                Response.WriteLine("Possible reaons:");
                Response.WriteLine("* Link to this URL was a typo in the original source");
                Response.WriteLine("* This URL was excluded from crawling pr archiving via robots.txt");
                Response.WriteLine("* You found a bug in Delorean");
                Response.WriteLine("Options:");
                Response.WriteLine($"=> {RoutePaths.ViewCached(AttemptedUrl.RootUrl, AttemptedTime)} Try looking at the cached version of capsule's home page");
                return;
            }

            SnapshotReader reader = new SnapshotReader(Settings.Global.DataRoot + "Packs/");


            if (RawMode)
            {
                Response.Success(Snapshot.Meta); 
                Response.Write(reader.ReadBytes(Snapshot));
                return;
            }

            var text = reader.ReadText(Snapshot);
            Response.Success();
            Response.WriteLine($"> This is the archive verision of {Snapshot.Url.FullUrl} as seen by the Kennedy Crawler on {Snapshot.Captured.ToString("yyyy-MM-dd")}");
            Response.WriteLine($"=> /delorean?{HttpUtility.UrlEncode(Snapshot.Url.FullUrl)} More Information in 🏎 Delorean Time Machine");
            Response.WriteLine($"=> {RoutePaths.ViewCached(Snapshot, true)} View Raw");
            Response.WriteLine();

            GemtextRewriter gemtextRewriter = new GemtextRewriter();
            var newText = gemtextRewriter.Rewrite(Snapshot, text);

            Response.Write(newText);                
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
                if (!DateTime.TryParse(args["t"], out AttemptedTime))
                {
                    AttemptedTime = DateTime.Now;
                }
                Snapshot = GetSnapshot(AttemptedUrl.ProperID, AttemptedTime);
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
