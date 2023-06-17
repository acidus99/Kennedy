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

            //support someone pasting in a full URL
            if(Domain.Contains("://"))
            {
                try
                {
                    Uri url = new Uri(Domain);
                    Domain = url.Host;
                }
                catch (Exception)
                {
                }
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
            Response.WriteLine("Click on any URL to see more info, including incoming links to that URL.");

            RenderNetworkErrors(docs);
            RenderPageErrors(docs);
            RenderGonePage(docs);            
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

        private void RenderNetworkErrors(IQueryable<Document> docs)
        {
            Response.WriteLine($"## Connectivity Issues");
            Response.WriteLine("This checks for any DNS, TLS, connection, or timeout issues.");

            //find connectivity problems
            var networkErrors = docs.Where(x => !x.IsAvailable)
                                        .OrderBy(x => x.Meta)
                                        .ThenBy(x => x.Url);
            var count = networkErrors.Count();

            if (count == 0)
            {
                Response.WriteLine("* 👏 Nice! No problems found.");
            }
            else
            {
                string meta = "";
                foreach (var doc in networkErrors)
                {
                    if (doc.Meta != meta)
                    {
                        meta = doc.Meta;
                        Response.WriteLine();
                        Response.WriteLine($"### {doc.Meta}");
                    }
                    Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(doc.GeminiUrl)} {doc.GeminiUrl}");
                }
            }
            Response.WriteLine();
        }

        private void RenderPageErrors(IQueryable<Document> docs)
        {
            Response.WriteLine("## Broken or Missing URLs");
            Response.WriteLine("This checks for any URLs with 4x or 5x status codes, indicating a broken or missing resource.");

            var pageErrors = docs.Where(x => x.Domain == Domain &&
                             x.IsAvailable &&
                             x.StatusCode >= 40 &&
                             x.StatusCode < 60 && x.StatusCode != 52)
                    .OrderBy(x => x.StatusCode)
                    .ThenBy(x => x.Url);
            var count = pageErrors.Count();

            if(count == 0)
            {
                Response.WriteLine("* 👏 Nice! No problems found.");
            }
            else
            {
                Response.WriteLine($"* URLs with problems: {count}");
                int statusCode = 0;

                foreach (var doc in pageErrors)
                {
                    if (doc.StatusCode != statusCode)
                    {
                        statusCode = doc.StatusCode;
                        Response.WriteLine();
                        Response.WriteLine($"### Statue Code {doc.StatusCode}");
                    }
                    Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(doc.GeminiUrl)} {doc.GeminiUrl}");
                }
            }           
        }

        private void RenderGonePage(IQueryable<Document> docs)
        {
            Response.WriteLine("## Gone URLs");
            Response.WriteLine("This checks for any URLs returing a \"52 GONE\" status code. " +
                "This incidates that the resource is purposely missing. Capsules " +
                "should use this when they remove content, but it's also possible " +
                "there was a mistake with these URLs. You should review and verify " +
                "these URLs are supposed to send a \"52 GONE\" status code.");

            var gonePages = docs.Where(x => x.Domain == Domain &&
                              x.IsAvailable &&
                              x.StatusCode == 52)
                              .OrderBy(x => x.Url);
            var count = 0;
            if (count == 0)
            {
                Response.WriteLine("* 👏 Nice! No problems found.");
            }
            else
            {
                Response.WriteLine($"* URLs with \"52 GONE\" status code: {count}");
                foreach (var doc in gonePages)
                {
                    Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(doc.GeminiUrl)} {doc.GeminiUrl}");
                }
            }            
        }
    }
}
