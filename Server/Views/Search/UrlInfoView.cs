using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gemini.Net;
using Kennedy.Data;
using Kennedy.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Search;

internal class UrlInfoView : AbstractView
{
    public UrlInfoView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    private UrlRecord? _urlEntry;
    private DocumentRecord? _docEntry;
    private DocumentImageRecord? _imageEntry;

    public override void Render()
    {
        var url = GeminiUrl.MakeUrl(SanitizedQuery);
        if (url == null)
        {
            Response.Redirect(RoutePaths.UrlInfoRoute);
            return;
        }

        var options = new DbContextOptionsBuilder<KennedyDbContext>()
            .UseSqlite($"Data Source={Settings.Global.SearchDbFile}")
            .Options;

        using var db = new KennedyDbContext(options);
        _urlEntry = db.UrlRegistry.FirstOrDefault(x => x.NormalizedUrl == url.NormalizedUrl);
        _docEntry = _urlEntry != null
            ? db.Documents.FirstOrDefault(x => x.UrlRegistryId == _urlEntry.Id)
            : null;
        _imageEntry = _urlEntry != null
            ? db.Images.FirstOrDefault(x => x.UrlRegistryId == _urlEntry.Id)
            : null;

        if (_urlEntry == null && _docEntry == null)
        {
            RenderUnknownUrl(url);
            return;
        }

        var canonicalUrl = _docEntry?.CanonicalUrl ?? _urlEntry?.NormalizedUrl ?? url.NormalizedUrl;
        var canonicalGeminiUrl = new GeminiUrl(canonicalUrl);

        Response.Success();
        Response.WriteLine($"# ℹ️ {FormatUrl(canonicalGeminiUrl)}");
        Response.WriteLine($"=> {canonicalUrl} Visit Current Url");
        Response.WriteLine($"=> {RoutePaths.ViewMostRecentCached(canonicalGeminiUrl)} View most recent cached version");
        Response.WriteLine($"=> {RoutePaths.ViewUrlUniqueHistory(canonicalGeminiUrl)} View all archived copies and history with 🏎 DeLorean Time Machine");
        Response.WriteLine($"=> {canonicalGeminiUrl.RootUrl} Capsule: {canonicalGeminiUrl.Hostname}");
        Response.WriteLine();

        RenderMetadata(canonicalGeminiUrl);
        RenderFileMetaData(db);
        RenderLinks(db, canonicalGeminiUrl);
    }

    private void RenderMetadata(GeminiUrl canonicalUrl)
    {
        Response.WriteLine("## Metadata");
        Response.WriteLine($"* {canonicalUrl}");

        if (_urlEntry == null)
        {
            Response.WriteLine("* No crawl metadata available for this URL.");
            return;
        }

        var statusCode = _urlEntry.LastStatusCode ?? 0;
        if (statusCode == GeminiParser.ConnectionErrorStatusCode)
        {
            Response.WriteLine($"* Connection Error: {_urlEntry.Meta}");
            return;
        }

        if (_urlEntry.LastStatusCode != null)
        {
            Response.WriteLine("* Response Line:");
            Response.WriteLine("```");
            Response.WriteLine($"{_urlEntry.LastStatusCode} {_urlEntry.Meta}");
            Response.WriteLine("```");
        }

        if (GeminiParser.IsSuccessStatus(statusCode) && _docEntry != null)
        {
            Response.WriteLine($"* Mimetype: {_urlEntry!.LastMimeType}");

            if (_docEntry.Language != null)
            {
                Response.WriteLine($"* Language: {FormatLanguage(_docEntry.Language)}");
            }

            if (!_docEntry.IsBodyTruncated)
            {
                Response.WriteLine($"* Size: {FormatSize(_docEntry.BodySize)}");
            }
            else
            {
                Response.WriteLine($"* Size: > {FormatSize(_docEntry.BodySize)}. The exact size is unknown since it exceeded our download limit.");
            }
        }

        Response.WriteLine($"* First Seen: {_urlEntry.FirstSeen:yyyy-MM-dd}");
        Response.WriteLine($"* Indexed on: {_urlEntry.LastSuccess?.ToString("yyyy-MM-dd")}");
    }

    private void RenderFileMetaData(KennedyDbContext db)
    {
        if (_urlEntry == null)
        {
            return;
        }

        if (_docEntry != null && (_docEntry.ContentType == ContentType.Gemtext || _docEntry.ContentType == ContentType.PlainText))
        {
            Response.WriteLine("### Text Metadata");
            if (_docEntry.ContentType == ContentType.Gemtext)
            {
                var title = _docEntry.Title ?? "(Could not determine)";
                Response.WriteLine($"* Title: {title}");
            }

            var language = _docEntry.DetectedLanguage != null ? FormatLanguage(_docEntry.DetectedLanguage) : "(Could not determine)";
            Response.WriteLine($"* Detected language: {language}");
            if (_docEntry.LineCount != null)
            {
                Response.WriteLine($"* Lines: {_docEntry.LineCount}");
            }
        }

        if (_imageEntry != null)
        {
            Response.WriteLine("### Image Metadata");
            if (_imageEntry.Width > 0 && _imageEntry.Height > 0)
            {
                Response.WriteLine($"* Dimensions: {_imageEntry.Width} x {_imageEntry.Height}");
            }

            if (!string.IsNullOrWhiteSpace(_imageEntry.ImageType))
            {
                Response.WriteLine($"* Format: {_imageEntry.ImageType.ToUpperInvariant()}");
            }

            var indexText = ReadFileIndexText(db, _urlEntry.Id);
            if (!string.IsNullOrWhiteSpace(indexText))
            {
                Response.WriteLine("* Indexable text:");
                Response.WriteLine($">{indexText}");
            }
        }
    }

    private static string? ReadFileIndexText(KennedyDbContext db, long urlId)
    {
        using var connection = new SqliteConnection(db.Database.GetConnectionString());
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT SearchText FROM FilesFts WHERE rowid = $rowid LIMIT 1;";
        cmd.Parameters.AddWithValue("$rowid", urlId);

        var value = cmd.ExecuteScalar();
        return value as string;
    }

    private void RenderLinks(KennedyDbContext db, GeminiUrl currentUrl)
    {
        if (_urlEntry == null)
        {
            return;
        }

        Response.WriteLine("## Links");

        var inboundInternal = (from links in db.UrlLinks
                               join sourceUrl in db.UrlRegistry on links.SourceUrlId equals sourceUrl.Id
                               join sourceDoc in db.Documents on (long?)sourceUrl.Id equals sourceDoc.UrlRegistryId into docs
                               from sourceDoc in docs.DefaultIfEmpty()
                               where links.TargetUrlId == _urlEntry.Id && !links.IsExternal
                               orderby sourceUrl.NormalizedUrl
                               select new LinkItem
                               {
                                   Url = sourceUrl.NormalizedUrl,
                                   Title = sourceDoc != null ? sourceDoc.Title : null,
                                   LinkText = links.LinkText
                               }).ToList();

        if (inboundInternal.Count > 0)
        {
            Response.WriteLine("### Internal Inbound Links");
            Response.WriteLine($"{inboundInternal.Count} inbound links, from other pages on {currentUrl.Hostname}.");
            RenderLinkItems(inboundInternal, "From", currentUrl.Hostname);
            Response.WriteLine();
        }

        var inboundExternal = (from links in db.UrlLinks
                               join sourceUrl in db.UrlRegistry on links.SourceUrlId equals sourceUrl.Id
                               join sourceDoc in db.Documents on (long?)sourceUrl.Id equals sourceDoc.UrlRegistryId into docs
                               from sourceDoc in docs.DefaultIfEmpty()
                               where links.TargetUrlId == _urlEntry.Id && links.IsExternal
                               orderby sourceUrl.NormalizedUrl
                               select new LinkItem
                               {
                                   Url = sourceUrl.NormalizedUrl,
                                   Title = sourceDoc != null ? sourceDoc.Title : null,
                                   LinkText = links.LinkText
                               }).ToList();

        if (inboundExternal.Count > 0)
        {
            Response.WriteLine("### External Inbound Links");
            Response.WriteLine($"{inboundExternal.Count} inbound links from other capsules.");
            RenderLinkItems(inboundExternal, "From", currentUrl.Hostname);
            Response.WriteLine();
        }

        var outbound = (from links in db.UrlLinks
                        join targetUrl in db.UrlRegistry on links.TargetUrlId equals targetUrl.Id
                        join targetDoc in db.Documents on (long?)targetUrl.Id equals targetDoc.UrlRegistryId into docs
                        from targetDoc in docs.DefaultIfEmpty()
                        where links.SourceUrlId == _urlEntry.Id
                        orderby targetUrl.NormalizedUrl
                        select new LinkItem
                        {
                            Url = targetUrl.NormalizedUrl,
                            Title = targetDoc != null ? targetDoc.Title : null,
                            LinkText = links.LinkText
                        }).ToList();

        if (outbound.Count > 0)
        {
            Response.WriteLine("### Outbound Links");
            Response.WriteLine($"{outbound.Count} outbound links from this page.");
            RenderLinkItems(outbound, "To", currentUrl.Hostname);
            Response.WriteLine();
        }
    }

    private void RenderLinkItems(IEnumerable<LinkItem> links, string direction, string currentHost)
    {
        int counter = 0;
        foreach (var link in links)
        {
            counter++;
            Response.WriteLine($"=> {link.Url} {counter}. {FormatLink(direction, link.Url, link.Title, link.LinkText, currentHost)}");
        }
    }

    private static string FormatLink(string direction, string url, string? pageTitle, string? linkText, string currentHost)
    {
        StringBuilder sb = new();
        sb.Append(direction);
        sb.Append(' ');

        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            sb.Append($"page titled '{pageTitle}'");
        }
        else
        {
            var targetUrl = new GeminiUrl(url);
            if (targetUrl.Hostname != currentHost)
            {
                sb.Append(targetUrl.Hostname);
            }

            sb.Append(targetUrl.Path);
        }

        if (!string.IsNullOrWhiteSpace(linkText))
        {
            sb.Append($" with link '{linkText}'");
        }

        return sb.ToString();
    }

    private void RenderUnknownUrl(GeminiUrl url)
    {
        Response.Success();
        Response.WriteLine("# ℹ️ Page Info");
        Response.WriteLine("Sorry, Kennedy has no information about this URL:");
        Response.WriteLine("```");
        Response.WriteLine($"{url}");
        Response.WriteLine("```");
        Response.WriteLine($"=> {RoutePaths.UrlInfoRoute} Try another URL");
    }

    private class LinkItem
    {
        public required string Url { get; set; }
        public string? Title { get; set; }
        public string? LinkText { get; set; }
    }
}
