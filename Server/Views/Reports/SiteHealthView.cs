using Kennedy.Data;
using Kennedy.Data.Models;
using Gemini.Net;
using Microsoft.EntityFrameworkCore;
using RocketForce;
using System.Linq;
using System;

namespace Kennedy.Server.Views.Reports;

internal class SiteHealthView : AbstractView
{
    public SiteHealthView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    private string Domain = "";

    public override void Render()
    {
        Domain = SanitizedQuery;
        Response.Success();

        if (string.IsNullOrWhiteSpace(Domain))
        {
            RenderUnknownDomain();
            return;
        }

        if (Domain.Contains("://"))
        {
            try
            {
                Uri url = new(Domain);
                Domain = url.Host;
            }
            catch
            {
                // Keep original value if parsing fails.
            }
        }

        Domain = Domain.ToLowerInvariant();

        var options = new DbContextOptionsBuilder<KennedyDbContext>()
            .UseSqlite($"Data Source={Settings.Global.SearchDbFile}")
            .Options;
        using var db = new KennedyDbContext(options);

        var urls = db.UrlRegistry.Where(x => x.Host == Domain);
        var totalUrls = urls.Count();

        if (totalUrls == 0)
        {
            RenderUnknownDomain();
            return;
        }

        Response.WriteLine($"# {Domain} - 🩺 Capsule Health Report");
        Response.WriteLine($"* Total URLs: {totalUrls}");
        Response.WriteLine("Click on any URL to see more info, including incoming links to that URL.");

        RenderNetworkErrors(urls);
        RenderPageErrors(urls);
        RenderGonePages(urls);
    }

    private void RenderUnknownDomain()
    {
        Response.WriteLine("# 🩺 Capsule Health Report");
        Response.WriteLine("Sorry, Kennedy has no information about this domain:");
        Response.WriteLine("```");
        Response.WriteLine(Domain);
        Response.WriteLine("```");
        Response.WriteLine($"=> {RoutePaths.SiteHealthRoute} Try another Domain");
    }

    private void RenderNetworkErrors(IQueryable<UrlRecord> urls)
    {
        Response.WriteLine("## Connectivity Issues");
        Response.WriteLine("This checks for any DNS, TLS, connection, or timeout issues.");

        var networkErrors = urls.Where(x => x.LastStatusCode == GeminiParser.ConnectionErrorStatusCode)
            .OrderBy(x => x.Meta)
            .ThenBy(x => x.NormalizedUrl)
            .ToList();

        if (networkErrors.Count == 0)
        {
            Response.WriteLine("* 👏 Nice! No problems found.");
        }
        else
        {
            string meta = "";
            foreach (var url in networkErrors)
            {
                if (url.Meta != meta)
                {
                    meta = url.Meta;
                    Response.WriteLine();
                    Response.WriteLine($"### {url.Meta}");
                }

                Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(new GeminiUrl(url.NormalizedUrl))} {url.NormalizedUrl}");
            }
        }

        Response.WriteLine();
    }

    private void RenderPageErrors(IQueryable<UrlRecord> urls)
    {
        Response.WriteLine("## Broken or Missing URLs");
        Response.WriteLine("This checks for any URLs with 4x or 5x status codes, indicating a broken or missing resource.");

        var pageErrors = urls.Where(x => x.LastStatusCode >= 40 && x.LastStatusCode < 60 && x.LastStatusCode != 52)
            .OrderBy(x => x.LastStatusCode)
            .ThenBy(x => x.NormalizedUrl)
            .ToList();

        if (pageErrors.Count == 0)
        {
            Response.WriteLine("* 👏 Nice! No problems found.");
        }
        else
        {
            Response.WriteLine($"* URLs with problems: {pageErrors.Count}");
            int statusCode = 0;
            foreach (var url in pageErrors)
            {
                if (url.LastStatusCode != statusCode)
                {
                    statusCode = url.LastStatusCode ?? 0;
                    Response.WriteLine();
                    Response.WriteLine($"### Status Code {url.LastStatusCode}");
                }

                Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(new GeminiUrl(url.NormalizedUrl))} {url.NormalizedUrl}");
            }
        }
    }

    private void RenderGonePages(IQueryable<UrlRecord> urls)
    {
        Response.WriteLine("## Gone URLs");
        Response.WriteLine("This checks for any URLs returning a \"52 GONE\" status code.");

        var gonePages = urls.Where(x => x.LastStatusCode == 52)
            .OrderBy(x => x.NormalizedUrl)
            .ToList();

        if (gonePages.Count == 0)
        {
            Response.WriteLine("* 👏 Nice! No problems found.");
        }
        else
        {
            Response.WriteLine($"* URLs with \"52 GONE\" status code: {gonePages.Count}");
            foreach (var url in gonePages)
            {
                Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(new GeminiUrl(url.NormalizedUrl))} {url.NormalizedUrl}");
            }
        }
    }
}
