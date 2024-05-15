using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Search;

internal class UrlInfoView : AbstractView
{
    public UrlInfoView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    WebDatabaseContext db = new WebDatabaseContext(Settings.Global.DataRoot);
    Document entry = null!;

    public override void Render()
    {
        var url = GeminiUrl.MakeUrl(SanitizedQuery);
        if (url == null)
        {
            Response.Redirect(RoutePaths.UrlInfoRoute);
            return;
        }

        Response.Success();

        Document? possibleEntry = db.Documents
            .Where(x => x.UrlID == url.ID)
            .Include(x => x.Image!)
            //.Include(x => x.Favicon)
            .FirstOrDefault()!;

        if (possibleEntry == null)
        {
            RenderUnknownUrl(url);
            return;
        }
        entry = possibleEntry;

        Response.WriteLine($"# ℹ️ {FormatUrl(entry.GeminiUrl)}");
        Response.WriteLine($"=> {entry.Url} Visit Current Url");
        Response.WriteLine($"=> {RoutePaths.ViewMostRecentCached(entry.GeminiUrl)} View most recent cached version");
        Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistory(entry.GeminiUrl)} View all archived copies and history with 🏎 DeLorean Time Machine");
        //var emoji = entry.Favicon?.Emoji + " " ?? "";
        var emoji = "";
        Response.WriteLine($"=> {entry.GeminiUrl.RootUrl} Capsule: {emoji}{entry.GeminiUrl.Hostname}");
        Response.WriteLine();

        RenderMetadata();
        RenderFileMetaData();
        RenderLinks();
    }

    private void RenderMetadata()
    {
        Response.WriteLine($"## Metadata");
        Response.WriteLine($"* {entry.GeminiUrl}");
        if (entry.StatusCode == GeminiParser.ConnectionErrorStatusCode)
        {
            Response.WriteLine($"* Connection Error: {entry.Meta}");
            return;
        }

        Response.WriteLine("* Response Line:");
        Response.WriteLine("```");
        Response.WriteLine($"{entry.StatusCode} {entry.Meta}");
        Response.WriteLine("```");

        if (GeminiParser.IsSuccessStatus(entry.StatusCode))
        {
            Response.WriteLine($"* Mimetype: {entry.MimeType}");
            if (entry.Charset != null)
            {
                Response.WriteLine($"* Charset: {entry.Charset}");
            }
            if (entry.Language != null)
            {
                Response.WriteLine($"* Language: {FormatLanguage(entry.Language)}");
            }

            if (!entry.IsBodyTruncated)
            {
                Response.WriteLine($"* Size: {FormatSize(entry.BodySize)}");
            }
            else
            {
                Response.WriteLine($"* Size: > {FormatSize(entry.BodySize)}. The exact size is unknown since it exceeded our download limit.");
            }
        }
        Response.WriteLine($"* First Seen: {entry.FirstSeen.ToString("yyyy-MM-dd")}");
        Response.WriteLine($"* Indexed on: {entry.LastSuccessfulVisit?.ToString("yyyy-MM-dd")}");
    }

    private void RenderFileMetaData()
    {
        switch (entry.ContentType)
        {
            case ContentType.Gemtext:
            case ContentType.PlainText:
                RenderTextMetaData();
                break;

            case ContentType.Image:
                RenderImageMetaData();
                break;
        }
    }

    private void RenderTextMetaData()
    {
        Response.WriteLine("### Text Metadata");

        if (entry.ContentType == ContentType.Gemtext)
        {
            var title = entry.Title ?? "(Could not determine)";
            Response.WriteLine($"* Title: {title}");
        }

        var language = (entry.DetectedLanguage != null) ? FormatLanguage(entry.DetectedLanguage) : "(Could not determine)";
        Response.WriteLine($"* Detected language: {language}");

        if (entry.LineCount != null)
        {
            Response.WriteLine($"* Lines: {entry.LineCount}");
        }
    }

    private void RenderImageMetaData()
    {
        if (entry.Image == null)
        {
            return;
        }

        Response.WriteLine("### Image Metadata");
        Response.WriteLine($"* Dimensions: {entry.Image.Width} x {entry.Image.Height}");
        Response.WriteLine($"* Format: {entry.Image.ImageType.ToUpper()}");

        var searchEngine = new SearchDatabase(Settings.Global.DataRoot);
        var terms = searchEngine.GetImageIndexText(entry.UrlID);

        if (terms != null)
        {
            Response.WriteLine($"* Indexable text:");
            Response.WriteLine($">{terms}");
        }
    }

    private void RenderLinks()
    {
        Response.WriteLine($"## Links");

        var tmplinks = (from links in db.Links
                        where links.TargetUrlID == entry.UrlID && !links.IsExternal
                        join docs in db.Documents on links.SourceUrlID equals docs.UrlID
                        orderby docs.Url
                        select new
                        LinkItem
                        {
                            Url = docs.Url,
                            Title = docs.Title,
                            LinkText = links.LinkText
                        }).ToList();
        if (tmplinks.Count > 0)
        {
            Response.WriteLine($"### Internal Inbound Links");
            Response.WriteLine($"{tmplinks.Count} inbound links, from other pages on {entry.GeminiUrl.Hostname}.");
            RenderLinks(tmplinks, "From");
            Response.WriteLine();
        }

        tmplinks = (from links in db.Links
                    where links.TargetUrlID == entry.UrlID && links.IsExternal
                    join docs in db.Documents on links.SourceUrlID equals docs.UrlID
                    orderby docs.Url
                    select new
                    LinkItem
                    {
                        Url = docs.Url,
                        Title = docs.Title,
                        LinkText = links.LinkText
                    }).ToList();

        if (tmplinks.Count > 0)
        {
            Response.WriteLine($"### External Inbound Links");
            Response.WriteLine($"{tmplinks.Count} inbound links from other capsules.");
            RenderLinks(tmplinks, "From");
            Response.WriteLine();
        }

        if (entry.OutboundLinks > 0)
        {
            tmplinks = (from links in db.Links
                        where links.SourceUrlID == entry.UrlID
                        join docs in db.Documents on links.TargetUrlID equals docs.UrlID
                        select new LinkItem
                        {
                            Url = docs.Url,
                            Title = docs.Title,
                            LinkText = links.LinkText
                        }).ToList();

            Response.WriteLine($"### Outbound Links");
            Response.WriteLine($"{tmplinks.Count} outbound links from this page.");
            RenderLinks(tmplinks, "To");
            Response.WriteLine();
        }
    }

    private void RenderLinks(IEnumerable<LinkItem> links, string direction)
    {
        int counter = 0;
        if (links.Count() > 0)
        {
            foreach (var link in links)
            {
                counter++;
                Response.WriteLine($"=> {link.Url} {counter}. {FormatLink(direction, link.Url, link.Title, link.LinkText)}");
            }
        }
    }

    private string FormatLink(string direction, string url, string? pageTitle, string? linkText)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(direction);
        sb.Append(' ');
        if (pageTitle?.Length > 0)
        {
            sb.Append($"page titled '{pageTitle}'");
        }
        else
        {
            var targetUrl = new GeminiUrl(url);
            //only include hostname if it's a different capsule
            if (targetUrl.Hostname != entry.Domain)
            {
                sb.Append(targetUrl.Hostname);
            }
            sb.Append(targetUrl.Path);
        }
        if (linkText?.Length > 0)
        {
            sb.Append($" with link '{linkText}'");
        }
        return sb.ToString();
    }

    private void RenderUnknownUrl(GeminiUrl url)
    {
        Response.WriteLine($"# ℹ️ Page Info");
        Response.WriteLine("Sorry, Kennedy has no information about this URL:");
        Response.WriteLine($"```");
        Response.WriteLine($"{url}");
        Response.WriteLine($"```");
        Response.WriteLine($"=> {RoutePaths.UrlInfoRoute} Try another URL");
    }

    private class LinkItem
    {
        public required string Url { get; set; }

        public string? Title { get; set; }

        public string? LinkText { get; set; }
    }
}