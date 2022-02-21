using System;
using System.Linq;

using Gemini.Net;
using Kennedy.CrawlData;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class DorleanResultView :AbstractView
    {

        public DorleanResultView(Request request, Response response, App app)
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

                var dbDocID = DocumentIndex.toLong(url.DocID);
                var db = new DocumentIndex(Settings.Global.DataRoot).GetContext();

                var entry = db.DocEntries.Where(x => x.DBDocID == dbDocID).FirstOrDefault();

                if (entry != null && entry.BodySaved)
                {
                    Response.Redirect($"/cached?id={entry.DBDocID}");
                    return;
                }
            }
            catch (Exception)
            {
            }

            Response.Success();
            Response.WriteLine($"# 🏎 DeLorean Time Machine");
            Response.WriteLine();
            Response.WriteLine("There are no cached versions of that URL.");
            Response.WriteLine("=> /delorean View Cached Content");
            Response.WriteLine("=> /search 🔭 New Search");
        }

    }
}
