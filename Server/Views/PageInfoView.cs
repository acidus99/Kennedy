using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Kennedy.Data;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class PageInfoView :AbstractView
    {
        public PageInfoView(Request request, Response response, App app)
            : base(request, response, app) { }

        private DocIndexDbContext db;
        private DocumentIndex documentIndex;
        StoredDocEntry entry;

        public override void Render()
        {
            documentIndex = new DocumentIndex(Settings.Global.DataRoot);

            db = documentIndex.GetContext();
            entry = null;
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

            var url = new GeminiUrl(entry.Url);

            Response.Success();

            Response.WriteLine($"# Page Info: {url.Path}");
            Response.WriteLine($"=> {entry.Url} Visit Current Url");

            if (entry.BodySaved)
            {
                Response.WriteLine($"=> /cached?id={dbDocID} View Cached copy (saved {entry.LastSuccessfulVisit?.ToString("yyyy-MM-dd")})");
            }

            Response.WriteLine();
            Response.WriteLine($"## Metadata");
            Response.WriteLine($"* Type: {entry.ContentType.ToString()}");
            Response.WriteLine($"* Size: {FormatSize(entry.BodySize)}");
            Response.WriteLine($"* Indexed on: {entry.LastSuccessfulVisit?.ToString("yyyy-MM-dd")}");
            Response.WriteLine($"=> {url.RootUrl} Capsule: {url.Hostname}");

            switch (entry.ContentType)
            {
                case ContentType.Text:
                    var title = (entry.Title.Length > 0) ? entry.Title : "(Could not extract a title)";
                    var language = FormatLanguage(entry.Language);
                    language = (language.Length > 0) ? language : "(Could not detect a language)";
                    Response.WriteLine($"* Title: {title}");
                    Response.WriteLine($"* Language: {language}");
                    Response.WriteLine($"* Lines: {entry.LineCount}");
                    break;

                case ContentType.Image:

                    var imgmeta = (from img in db.ImageEntries
                                   where img.DBDocID == dbDocID
                                   select new
                                   {
                                       img.Height,
                                       img.Width,
                                       img.ImageType,
                                       img.IsTransparent,
                                   }).FirstOrDefault();

                    var terms = documentIndex.GetImageIndexText(dbDocID);

                    Response.WriteLine($"* Dimensions: {imgmeta.Width} x {imgmeta.Height}");
                    Response.WriteLine($"* Format: {imgmeta.ImageType}");
                    Response.WriteLine($"* Indexable text:");
                    Response.WriteLine($">{terms}");
                    break;
            }

            if (entry.MimeType.StartsWith("text/gemini"))
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

            var inboundLinks = (from links in db.LinkEntries
                                where links.DBTargetDocID == entry.DBDocID && !links.IsExternal
                                join docs in db.DocEntries on links.DBSourceDocID equals docs.DBDocID
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

            inboundLinks = (from links in db.LinkEntries
                                where links.DBTargetDocID == entry.DBDocID && links.IsExternal
                                join docs in db.DocEntries on links.DBSourceDocID equals docs.DBDocID
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

            var outboundLinks = (from links in db.LinkEntries
                                 where links.DBSourceDocID == entry.DBDocID
                                 join docs in db.DocEntries on links.DBTargetDocID equals docs.DBDocID
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
            var inboundLinks = (from links in db.LinkEntries
                                where links.DBTargetDocID == entry.DBDocID && !links.IsExternal
                                join docs in db.DocEntries on links.DBSourceDocID equals docs.DBDocID
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

            inboundLinks = (from links in db.LinkEntries
                                where links.DBTargetDocID == entry.DBDocID && links.IsExternal
                                join docs in db.DocEntries on links.DBSourceDocID equals docs.DBDocID
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

            var outboundLinks = (from links in db.LinkEntries
                                 where links.DBSourceDocID == entry.DBDocID
                                 join docs in db.DocEntries on links.DBTargetDocID equals docs.DBDocID
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
            
            if(pageTitle.Length >0)
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
