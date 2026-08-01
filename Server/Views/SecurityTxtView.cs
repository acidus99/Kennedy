using System.Linq;
using Kennedy.Data;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views;

internal class SecurityTxtView : AbstractView
{
    public SecurityTxtView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        var options = new DbContextOptionsBuilder<KennedyDbContext>()
            .UseSqlite($"Data Source={Settings.Global.SearchDbFile}")
            .Options;

        using var db = new KennedyDbContext(options);

        Response.Success();
        Response.WriteLine("# 🔭 Capsules with security.txt ");
        Response.WriteLine("The following are capsules using the \"security.txt\" standard, allowing people to easily contact capsule owners about security issues.");
        Response.WriteLine("=> https://securitytxt.org About Security.txt");
        Response.WriteLine();

        var servers = (
            from securityTxt in db.SecurityTxts
            join favicon in db.Favicons
                on new { securityTxt.Scheme, securityTxt.Host, securityTxt.Port }
                equals new { favicon.Scheme, favicon.Host, favicon.Port } into matchingFavicons
            from favicon in matchingFavicons.DefaultIfEmpty()
            select new
            {
                securityTxt.Scheme,
                securityTxt.Host,
                securityTxt.Port,
                Favicon = favicon == null ? null : favicon.Emoji
            })
            .OrderBy(x => x.Host)
            .ThenBy(x => x.Port)
            .ToList();

        Response.WriteLine($"## Capsules with security.txt ({servers.Count})");

        int count = 0;
        foreach (var host in servers)
        {
            count++;
            var label = $"{count}. {FormatDomain(host.Host, host.Favicon)}";
            if (host.Port != 1965)
            {
                label += ":" + host.Port;
            }

            Response.WriteLine($"=> {host.Scheme}://{host.Host}:{host.Port}/.well-known/security.txt {label}");
        }
    }
}
