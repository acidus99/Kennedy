using System.Linq;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views;

internal class KnownHostsView : AbstractView
{
    public KnownHostsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        Response.Success();

        Response.WriteLine("# 🔭 Known Gemini Caspules");
        Response.WriteLine();
        Response.WriteLine("The following are capsules that:");
        Response.WriteLine("* Are known to Kennedy.");
        Response.WriteLine("* Resolve to an IP address.");
        Response.WriteLine("* Properly accept TLS connections");
        Response.WriteLine("* Send with a valid Gemini response.");

        var options = new DbContextOptionsBuilder<KennedyDbContext>()
            .UseSqlite($"Data Source={Settings.Global.SearchDbFile}")
            .Options;

        using var db = new KennedyDbContext(options);

        var servers = (
            from url in db.UrlRegistry
            join favicon in db.Favicons
                on new { url.Scheme, url.Host, url.Port }
                equals new { favicon.Scheme, favicon.Host, favicon.Port } into matchingFavicons
            from favicon in matchingFavicons.DefaultIfEmpty()
            where url.LastStatusCode != Gemini.Net.GeminiParser.ConnectionErrorStatusCode
            group url by new
            {
                url.Scheme,
                url.Host,
                url.Port,
                Favicon = favicon == null ? null : favicon.Emoji
            } into capsuleGroup
            select new
            {
                capsuleGroup.Key.Scheme,
                capsuleGroup.Key.Host,
                capsuleGroup.Key.Port,
                capsuleGroup.Key.Favicon,
                Pages = capsuleGroup.Count(),
                LastDate = capsuleGroup.Max(d => d.LastVisit)
            })
            .OrderBy(x => x.Host)
            .ThenBy(x => x.Port)
            .ToList();

        Response.WriteLine($"## Known Capsules ({servers.Count})");

        int counter = 0;
        foreach (var server in servers)
        {
            counter++;
            var label = $"{counter}. {FormatDomain(server.Host, server.Favicon)}";
            if (server.Port != 1965)
            {
                label += ":" + server.Port;
            }

            label += $" ({server.Pages} URLs. Updated: {server.LastDate})";
            Response.WriteLine($"=> {server.Scheme}://{server.Host}:{server.Port}/ {label}");
        }
    }
}
