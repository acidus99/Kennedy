using System;
using System.Linq;
using System.IO;
using System.Web;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.SearchIndex.Web;
using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Models;
using Kennedy.Data;
using RocketForce;

namespace Kennedy.Server.Views.Search
{
    internal class PageInfoView :AbstractView
    {
        public PageInfoView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        WebDatabaseContext db = new WebDatabaseContext(Settings.Global.DataRoot);
        Document entry = null!;

        public override void Render()
        {
            long urlID = 0;

            Document? possibleEntry = null;

            var query = SanitizedQuery;
            if (query.StartsWith("id=") && query.Length > 3)
            {
                urlID = Convert.ToInt64(query.Substring(3));
                possibleEntry = db.Documents.Where(x => x.UrlID == urlID).Include(x=>x.Image!).FirstOrDefault()!;
            }

            if (possibleEntry == null)
            {
                Response.Redirect("/");
                return;
            }
            entry = possibleEntry;

            Response.Success();

            Response.WriteLine($"# Page Info: {entry.GeminiUrl.Path}");
            Response.WriteLine($"=> {entry.Url} Visit Current Url");
            Response.WriteLine($"=> {RoutePaths.ViewUrlHistory(entry.GeminiUrl)} View archived copies with 🏎 DeLorean Time Machine");

            Response.WriteLine();
            Response.WriteLine($"## Metadata");
            Response.WriteLine($"* Type: {entry.ContentType.ToString()}");
            Response.WriteLine($"* Size: {FormatSize(entry.BodySize)}");
            Response.WriteLine($"* Indexed on: {entry.LastSuccessfulVisit?.ToString("yyyy-MM-dd")}");
            Response.WriteLine($"=> {entry.GeminiUrl.RootUrl} Capsule: {entry.GeminiUrl.Hostname}");

            switch (entry.ContentType)
            {
                case ContentType.Gemtext:
                    var title = entry.Title ?? "(Could not determine)";
                    var language = (entry.Language != null) ? FormatLanguage(entry.Language) : "(Could not determine)";
                    Response.WriteLine($"* Title: {title}");
                    Response.WriteLine($"* Language: {language}");

                    if (entry.LineCount != null)
                    {
                        Response.WriteLine($"* Lines: {entry.LineCount}");
                    }
                    break;

                case ContentType.Image:

                    var searchEngine = new SearchDatabase(Settings.Global.DataRoot);

                    var terms = searchEngine.GetImageIndexText(urlID);
                    if (entry.Image != null)
                    {
                        Response.WriteLine($"* Dimensions: {entry.Image.Width} x {entry.Image.Height}");
                        Response.WriteLine($"* Format: {entry.Image.ImageType}");
                        Response.WriteLine($"* Indexable text:");
                        Response.WriteLine($">{terms}");
                    }
                    break;
            }

            if (entry.MimeType != null && entry.MimeType.StartsWith("text/gemini"))
            {
                RenderGemtextLinks();
            }
            else
            {
                RenderOtherLinks();
            }
        }

        private void RenderGemtextLinks()
        {

            var inboundLinks = (from links in db.Links
                                where links.TargetUrlID == entry.UrlID && !links.IsExternal
                                join docs in db.Documents on links.SourceUrlID equals docs.UrlID
                                orderby docs.Url
                                select new
                                {
                                    docs.Url,
                                    docs.Title,
                                    links.LinkText
                                }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {inboundLinks.Count} Internal links to this content");
            int counter = 0;
            if (inboundLinks.Count > 0)
            {
                foreach (var link in inboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatLink("From", link.Url, link.Title, link.LinkText)}");
                }
            }
            else
            {
                Response.WriteLine("No internal links");
            }

            inboundLinks = (from links in db.Links
                                where links.TargetUrlID == entry.UrlID && links.IsExternal
                                join docs in db.Documents on links.SourceUrlID equals docs.UrlID
                                orderby docs.Url
                                select new
                                {
                                    docs.Url,
                                    docs.Title,
                                    links.LinkText
                                }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {inboundLinks.Count} Incoming links from other capsules");
            counter = 0;
            if (inboundLinks.Count > 0)
            {
                foreach (var link in inboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatLink("From", link.Url, link.Title, link.LinkText)}");
                }
            }
            else
            {
                Response.WriteLine("No incoming links");
            }

            var outboundLinks = (from links in db.Links
                                 where links.SourceUrlID == entry.UrlID
                                 join docs in db.Documents on links.TargetUrlID equals docs.UrlID
                                 select new
                                 {
                                     docs.Url,
                                     docs.Title,
                                     links.LinkText
                                 }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {outboundLinks.Count} Outgoing links");
            if (outboundLinks.Count > 0)
            {
                counter = 0;
                foreach (var link in outboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatLink("To", link.Url, link.Title, link.LinkText)}");
                }
            }
            else
            {
                Response.WriteLine("No outgoing links");
            }
        }

        private void RenderOtherLinks()
        {
            var inboundLinks = (from links in db.Links
                                where links.TargetUrlID == entry.UrlID && !links.IsExternal
                                join docs in db.Documents on links.SourceUrlID equals docs.UrlID
                                orderby docs.Url
                                select new
                                {
                                    docs.Url,
                                    docs.Title,
                                    links.LinkText
                                }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {inboundLinks.Count} Internal links to this content");
            int counter = 0;
            if (inboundLinks.Count > 0)
            {
                foreach (var link in inboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatLink("From", link.Url, link.Title, link.LinkText)}");
                }
            }
            else
            {
                Response.WriteLine("No internal links");
            }

            inboundLinks = (from links in db.Links
                                where links.TargetUrlID == entry.UrlID && links.IsExternal
                                join docs in db.Documents on links.SourceUrlID equals docs.UrlID
                                orderby docs.Url
                                select new
                                {
                                    docs.Url,
                                    docs.Title,
                                    links.LinkText
                                }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {inboundLinks.Count} Incoming links from other capsules");
            counter = 0;
            if (inboundLinks.Count > 0)
            {
                foreach (var link in inboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatLink("From", link.Url, link.Title, link.LinkText)}");
                }
            }
            else
            {
                Response.WriteLine("No incoming links");
            }

            var outboundLinks = (from links in db.Links
                                 where links.SourceUrlID == entry.UrlID
                                 join docs in db.Documents on links.TargetUrlID equals docs.UrlID
                                 select new
                                 {
                                     docs.Url,
                                     docs.Title,
                                     links.LinkText
                                 }).ToList();

            Response.WriteLine();
            Response.WriteLine($"## {outboundLinks.Count} Outgoing links");
            if (outboundLinks.Count > 0)
            {
                counter = 0;
                foreach (var link in outboundLinks)
                {
                    counter++;
                    Response.WriteLine($"=> {link.Url} {counter}. {FormatLink("To", link.Url, link.Title, link.LinkText)}");
                }
            }
            else
            {
                Response.WriteLine("No outgoing links");
            }
        }


        private string FormatLink(string direction, string url, string pageTitle, string linkText)
        {
            string s = direction + " ";
            
            if(pageTitle?.Length >0)
            {
                s += $"page titled '{pageTitle}'";
            } else
            {
                var u = new GeminiUrl(url);
                s += u.Hostname + u.Path;
            }
            if(linkText.Length > 0)
            {
                s += $" with link '{linkText}'";
            }
            return s;
        }

    }
}
