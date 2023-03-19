using System;
using System.Linq;
using System.Web;
using Gemini.Net;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.CrawlData;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class DorleanResultView :AbstractView
    {

        public DorleanResultView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            string query = SanitizedQuery;
            GeminiUrl url = null;
            try
            {
                url = new GeminiUrl(query);

            } catch(Exception)
            {
                Response.Success();
                Response.WriteLine($"# 🏎 DeLorean Time Machine");
                Response.WriteLine();
                Response.WriteLine("Invalid URL. Please enter a fully qualified, valid, Gemini URL.");
                Response.WriteLine("=> /delorean Try Again");
                return;
            }

            try
            {
                var dbDocID = DocumentIndex.toLong(url.HashID);

                ArchiveDbContext db = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");

                var urlEntry = db.Urls.Where(x => x.Id == dbDocID).Include(x => x.Snapshots).FirstOrDefault();

                if (urlEntry == null)
                {
                    Response.Success();
                    Response.WriteLine($"# 🏎 DeLorean Time Machine");
                    Response.WriteLine("No snapshots for that URL");
                    Response.WriteLine();
                    Response.WriteLine("=> /delorean Search Delorean for cached Content");
                    Response.WriteLine("=> /search 🔭 New Kennedy Search");
                    return;
                }

                Response.Success();
                Response.WriteLine($"# 🏎 DeLorean Time Machine");
                Response.WriteLine();
                Response.WriteLine("Found this URL in time machine!");
                Response.WriteLine($"URL: {urlEntry.GeminiUrl.NormalizedUrl}");

                var snapshots = urlEntry.Snapshots.ToArray();

                for(int i=0; i < snapshots.Length; i++)
                {

                    var snapshot = snapshots[i];

                    var hash = string.Format("{0:X}", snapshot.DataHash);
                    Response.WriteLine($"=> /cached-full?sid={snapshot.Id} {snapshot.Captured} {snapshot.Meta} {FormatSize(snapshot.Size)} {hash}");
                }
            }
            catch (Exception)
            {
            }
        }

    }
}
