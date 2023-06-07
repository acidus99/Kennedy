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
            
            Response.WriteLine($"# {Domain} - 🩺 Capsule Health Report");
            Response.WriteLine($"* Total URLs: {totalDocs}");

            //find connectivity problems
            var networkErrors = docs.Where(x => !x.IsAvailable)
                                        .OrderBy(x => x.Meta)
                                        .ThenBy(x=>x.Url);

            if (networkErrors.Count() > 0)
            {
                RenderNetworkErrors(networkErrors);
            }

            var pageErrors = docs.Where(x => x.Domain == Domain &&
                                            x.IsAvailable &&
                                            x.StatusCode >= 40 &&
                                            x.StatusCode < 60)
                                   .OrderBy(x => x.StatusCode);

            if(pageErrors.Count() > 0)
            {
                RenderPageErrors(pageErrors);
            }            
        }

        private void RenderUnknownDomain()
        {
            Response.WriteLine($"# 🩺 Capsule Health Report");
            Response.WriteLine("Sorry, Kennedy has no information about this domain:");
            Response.WriteLine($"```");
            Response.WriteLine($"{Domain}");
            Response.WriteLine($"```");
            Response.WriteLine($"=> {RoutePaths.SiteHealthRoute} Try another Domain");
        }

        private void RenderNetworkErrors(IEnumerable<Document> docsWithProblems)
        {
            string meta = "";
            Response.WriteLine($"## Connectivity Issues");
            Response.WriteLine($"* URLs with problems: {docsWithProblems.Count()}");

            foreach (var doc in docsWithProblems)
            {
                if(doc.Meta != meta)
                {
                    meta = doc.Meta;
                    Response.WriteLine();
                    Response.WriteLine($"### {doc.Meta}");
                }
                Response.WriteLine($"=> {doc.GeminiUrl}");
            }
            Response.WriteLine();
        }

        private void RenderPageErrors(IEnumerable<Document> pageErrors)
        {
            Response.WriteLine("## URL Issues");
            Response.WriteLine($"* URLs with problems: {pageErrors.Count()}");
            Response.WriteLine("Click URL to see more info, including incoming links.");

            int statusCode = 0;

            foreach (var doc in pageErrors)
            {
                if(doc.StatusCode != statusCode)
                {
                    statusCode = doc.StatusCode;
                    Response.WriteLine();
                    Response.WriteLine($"### Statue Code {doc.StatusCode}");
                }
                Response.WriteLine($"=> {RoutePaths.ViewPageInfo(doc.GeminiUrl)} {doc.GeminiUrl}");
            }
        }
    }
}
