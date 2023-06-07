using System;
using System.Linq;
using System.Collections.Generic;
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
                RenderUnknownDomain();
                return;
            }

            var db = new WebDatabaseContext(Settings.Global.DataRoot);


            var docs = db.Documents
                .Where(x => x.Domain == Domain);

            var totalDocs = docs.Count();

            if(totalDocs == 0)
            {
                RenderUnknownDomain();
                return;
            }
            
            Response.WriteLine($"# {Domain} - 🩺 Site Health Report");


            var docsWithProblems = docs.Where(x => x.Domain == Domain &&
                                            x.IsAvailable &&
                                            x.StatusCode >= 40 &&
                                            x.StatusCode < 60).ToArray();

            Response.WriteLine($"* Total URLs: {totalDocs}");
            Response.WriteLine($"* URLs with problems: {docsWithProblems.Count()}");

            Response.WriteLine("## Issues");
            int counter = 1;
            foreach (var doc in docsWithProblems)
            {
                var geminiUrl = new GeminiUrl(doc.Url);

                var links = db.Links.Include(x => x.SourceUrl)
                    .Where(x => x.TargetUrlID == doc.UrlID)
                    .OrderBy(x => x.SourceUrl!.Url);

                Response.WriteLine($"### Issue {counter}: Statue Code {doc.StatusCode} on {geminiUrl.Path} ");
                Response.WriteLine($"=> {doc.Url}");
                Response.WriteLine($"Incoming Links");

                int linkCounter = 0;
                foreach (var link in links)
                {
                    if (link.SourceUrl != null)
                    {
                        linkCounter++;
                        var linkLabel = !string.IsNullOrEmpty(link.LinkText) ?
                            $"Link \"{link.LinkText}\"" :
                            "Bare link";

                        Response.WriteLine($"=> {link.SourceUrl.Url} {linkCounter}. {linkLabel} on {link.SourceUrl.Url}");
                    }
                }

                Response.WriteLine();
                counter++;
            }
        }

        private void RenderUnknownDomain()
        {
            Response.WriteLine($"# 🩺 Site Health Report");
            Response.WriteLine("Sorry, Kennedy has no information about this domain:");
            Response.WriteLine($"```");
            Response.WriteLine($"{Domain}");
            Response.WriteLine($"```");
            Response.WriteLine($"=> {RoutePaths.SiteHealthRoute} Try another Domain");
        }
    }
}
