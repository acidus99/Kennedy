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

        var servers = db.UrlRegistry
            .Where(x => x.NormalizedUrl.Contains("/.well-known/security.txt") && x.LastStatusCode >= 20 && x.LastStatusCode < 30)
            .Select(x => new { x.Host, x.Port })
            .Distinct()
            .OrderBy(x => x.Host)
            .ThenBy(x => x.Port)
            .ToList();

        Response.WriteLine($"## Capsules with security.txt ({servers.Count})");

        int count = 0;
        foreach (var host in servers)
        {
            count++;
            var label = $"{count}. {host.Host}";
            if (host.Port != 1965)
            {
                label += ":" + host.Port;
            }

            Response.WriteLine($"=> gemini://{host.Host}:{host.Port}/.well-known/security.txt {label}");
        }
    }
}
