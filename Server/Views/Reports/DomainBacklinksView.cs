using System;
using System.Collections.Generic;
using System.Linq;
using Gemini.Net;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Reports;

internal class DomainBacklinksView : AbstractView
{
    public DomainBacklinksView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        var authority = ParseAuthority(SanitizedQuery);
        Response.Success();

        var options = new DbContextOptionsBuilder<KennedyDbContext>()
            .UseSqlite($"Data Source={Settings.Global.SearchDbFile}")
            .Options;
        using var db = new KennedyDbContext(options);

        if (!DomainExists(db, authority.protocol, authority.domain, authority.port))
        {
            RenderUnknownDomain(authority.domain);
            return;
        }

        Response.WriteLine($"# {authority.domain} - ↩️ Backlinks Report");
        Response.WriteLine($"Protocol: {authority.protocol}");
        Response.WriteLine($"Domain: {authority.domain}");
        Response.WriteLine($"Port: {authority.port}");

        var backlinks = GetBacklinks(db, authority.protocol, authority.domain, authority.port);

        Response.WriteLine($"Backlinks: {backlinks.Count}");

        string previousTarget = "";
        int urlGroupNumber = 0;
        int urlNumber = 0;
        for (int i = 0; i < backlinks.Count; i++)
        {
            var backlink = backlinks[i];

            if (!string.Equals(backlink.TargetUrl.NormalizedUrl, previousTarget, StringComparison.Ordinal))
            {
                urlNumber = 0;
                urlGroupNumber++;
                previousTarget = backlink.TargetUrl.NormalizedUrl;
                Response.WriteLine();
                Response.WriteLine($"## {urlGroupNumber}. Url: {backlink.TargetUrl.Path}");
                Response.WriteLine($"=> {backlink.TargetUrl} Full Url: {backlink.TargetUrl}");
                Response.WriteLine($"Status Code: {backlink.StatusCode}");
                Response.WriteLine($"{CountUrls(backlinks, i)} Backlinks:");
            }

            urlNumber++;
            Response.Write($"=> {backlink.SourceUrl} {urlNumber}. \"{backlink.SourceUrl}\"");
            if (!string.IsNullOrEmpty(backlink.LinkText))
            {
                Response.Write($" with link \"{backlink.LinkText}\"");
            }

            Response.WriteLine();
        }
    }

    private static bool DomainExists(KennedyDbContext db, string protocol, string domain, int port)
        => db.UrlRegistry.Any(x => x.Scheme == protocol && x.Host == domain && x.Port == port);

    private static List<Backlink> GetBacklinks(KennedyDbContext db, string protocol, string domain, int port)
    {
        return (from links in db.UrlLinks
                join source in db.UrlRegistry on links.SourceUrlId equals source.Id
                join target in db.UrlRegistry on links.TargetUrlId equals target.Id
                where links.IsExternal && target.Scheme == protocol && target.Host == domain && target.Port == port
                orderby target.NormalizedUrl
                select new Backlink
                {
                    SourceUrl = new GeminiUrl(source.NormalizedUrl),
                    TargetUrl = new GeminiUrl(target.NormalizedUrl),
                    StatusCode = target.LastStatusCode ?? 0,
                    LinkText = links.LinkText
                }).ToList();
    }

    private static (string protocol, string domain, int port) ParseAuthority(string value)
    {
        value = value.ToLowerInvariant();
        int index = value.IndexOf(':');
        if (index >= 1 && value.Length > index + 1)
        {
            try
            {
                return ("gemini", value.Substring(0, index), Convert.ToInt32(value.Substring(index + 1)));
            }
            catch
            {
            }
        }

        return ("gemini", value, 1965);
    }

    private void RenderUnknownDomain(string domain)
    {
        Response.WriteLine("# ↩️ Backlinks Report");
        Response.WriteLine("Sorry, Kennedy has no information about this domain:");
        Response.WriteLine("```");
        Response.WriteLine(domain);
        Response.WriteLine("```");
        Response.WriteLine($"=> {RoutePaths.DomainBacklinksRoute} Try another Domain");
    }

    private static int CountUrls(IReadOnlyList<Backlink> backlinks, int currentIndex)
    {
        int count = 0;
        string targetUrl = backlinks[currentIndex].TargetUrl.NormalizedUrl;
        for (; currentIndex < backlinks.Count; currentIndex++)
        {
            if (!string.Equals(targetUrl, backlinks[currentIndex].TargetUrl.NormalizedUrl, StringComparison.Ordinal))
            {
                break;
            }

            count++;
        }

        return count;
    }

    private class Backlink
    {
        public required GeminiUrl SourceUrl { get; set; }
        public required GeminiUrl TargetUrl { get; set; }
        public string? LinkText { get; set; }
        public required int StatusCode { get; set; }
    }
}
