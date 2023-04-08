using System;
using System.Linq;
using System.IO;
using System.Web;

using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.SearchIndex.Web;
using Kennedy.SearchIndex.Models;
using RocketForce;
using Kennedy.Archive.Db;
using Kennedy.Archive;

namespace Kennedy.Server.Views.Reports
{
    internal class SiteHealthView :AbstractView
    {
        public SiteHealthView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        string Domain = "";

        public override void Render()
        {
            Domain = SanitizedQuery;

            Response.Success();

            if (Domain == "")
            {
                Response.WriteLine("Unknown Domain");
                return;
            }

            var db = new WebDatabaseContext(Settings.Global.DataRoot);
            

            Response.WriteLine($"# {Domain} - 🩺 Site Health Report");

            var docs = db.Documents
                .Where(x => x.Domain == Domain);

            var totalDocs = docs.Count();

            var docsWithProblems = docs.Where(x => x.Domain == Domain &&
                                            x.ConnectStatus != ConnectStatus.Error &&
                                            x.Status >= 40 &&
                                            x.Status < 60).ToArray();

            Response.WriteLine($"* Total URLs: {totalDocs}");
            Response.WriteLine($"* URLs with problems: {docsWithProblems.Count()}");

            Response.WriteLine("## Issues");
            int counter = 1;
            foreach (var doc in docsWithProblems)
            {
                var geminiUrl = new GeminiUrl(doc.Url);

                Response.WriteLine($"### {counter} Code {doc.Status} on {geminiUrl.Path} ");
                Response.WriteLine($"=> {doc.Url}");
                Response.WriteLine("Incoming Links:");

                var links = db.Links.Include(x => x.SourceUrl)
                    .Where(x => x.TargetUrlID == doc.UrlID);

                foreach (var link in links)
                {
                    Response.WriteLine($"=> {link.SourceUrl.Url} Link \"{link.LinkText}\" on {link.SourceUrl.Url}");
                }

                Response.WriteLine();
                counter++;
            }
        }
    }
}
