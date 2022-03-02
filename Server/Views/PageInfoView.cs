using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class PageInfoView :AbstractView
    {
        public PageInfoView(Request request, Response response, App app)
            : base(request, response, app) { }

        public override void Render()
        {
            var db = (new DocumentIndex(Settings.Global.DataRoot)).GetContext();
            StoredDocEntry entry = null;
            long dbDocID = 0;

            var query = SanitizedQuery;
            if (query.StartsWith("id=") && query.Length > 3)
            {
                dbDocID = Convert.ToInt64(query.Substring(3));
                entry = db.DocEntries.Where(x => x.DBDocID == dbDocID).FirstOrDefault();
            }
            if (entry == null)
            {
                Response.Redirect("/");
                return;
            }

            Response.Success();

            Response.WriteLine($"# Page Info - Kennedy");
            Response.WriteLine($"=> {entry.Url} Visit Url");
            var title = (entry.Title.Length > 0) ? entry.Title : "(Could not extract a title)";
            var language = FormatLanguage(entry.Language);
            language = (language.Length > 0) ? language : "(Could not detect a language)";

            Response.WriteLine();
            Response.WriteLine($"## Metadata");
            Response.WriteLine($"* Title: {title}");
            Response.WriteLine($"* Language: {language}");
            Response.WriteLine($"* Lines: {entry.LineCount}");
            Response.WriteLine($"* Size: {FormatSize(entry.BodySize)}");

            if (entry.BodySaved)
            {
                Response.WriteLine($"=> /cached?id={entry.DBDocID} View Cached copy (saved {entry.LastSuccessfulVisit?.ToString("yyyy-MM-dd")})");
            }

            var inboundLinks = (from links in db.LinkEntries
                                where links.DBTargetDocID == dbDocID && links.IsExternal
                                join docs in db.DocEntries on links.DBSourceDocID equals docs.DBDocID
                                orderby docs.Url
                                select new
                                {
                                    docs.Url,
                                    docs.Title,
                                }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {inboundLinks.Count} Incoming links from other capsules");
            int counter = 0;
            if (inboundLinks.Count > 0)
            {
                foreach (var link in inboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatPageTitle(new GeminiUrl(link.Url), link.Title)}");
                }
            } else
            {
                Response.WriteLine("No incoming links");
            }

            var outboundLinks = (from links in db.LinkEntries
                                where links.DBSourceDocID == dbDocID
                                join docs in db.DocEntries on links.DBTargetDocID equals docs.DBDocID
                                select new
                                {
                                    docs.Url,
                                    docs.Title,
                                }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {outboundLinks.Count} Outgoing links");
            if (outboundLinks.Count > 0)
            {
                counter = 0;
                foreach (var link in outboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatPageTitle(new GeminiUrl(link.Url), link.Title)}");
                }
            } else
            {
                Response.WriteLine("No outgoing links");
            }
        }
    }
}
