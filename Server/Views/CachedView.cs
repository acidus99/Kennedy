using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class CachedView :AbstractView
    {

        public CachedView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {

            var query = SanitizedQuery;
            if(!query.StartsWith("id=") || query.Length < 4)
            {
                Response.Redirect("/");
                return;
            }
            try
            {
                var dbDocID = Convert.ToInt64(query.Substring(3));

                var db = new DocumentIndex(Settings.Global.DataRoot).GetContext();

                var entry = db.DocEntries.Where(x => x.DBDocID == dbDocID).FirstOrDefault();
                
                if(entry != null && entry.BodySaved)
                {
                    entry.SetDocID();
                    DocumentStore docStore = new DocumentStore(Settings.Global.DataRoot + "page-store/");

                    byte[] body = docStore.GetDocument(entry.DocID);

                    if (entry.MimeType.StartsWith("text/"))
                    {
                        Response.Success();
                        Response.WriteLine($"> This is the Cached verision of {entry.Url} as seen by the Kennedy Crawler on {entry.LastVisit.Value.ToString("yyyy-MM-dd")}");
                        Response.WriteLine($"=> {entry.Url} Current Version");
                        Response.WriteLine();
                        Response.Write(body);
                        return;
                    }
                    else
                    {
                        Response.Success(entry.MimeType);
                        Response.Write(body);
                    }
                }
            }
            catch(Exception)
            {
            }

            Response.Success();
            Response.WriteLine($"# 🏎 DeLorean Time Machine");
            Response.WriteLine("Unable to display a cached copy of that URL.");
            Response.WriteLine();
            Response.WriteLine("=> /search New Search");
            Response.WriteLine("=> /delorean View Cached Content");
            Response.WriteLine("=> / Home");
        }

    }
}
