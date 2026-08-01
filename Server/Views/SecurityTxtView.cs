using System.Linq;
using Kennedy.SearchIndex.Web;
using RocketForce;

namespace Kennedy.Server.Views;

internal class SecurityTxtView : AbstractView
{
    public SecurityTxtView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        using var db = new WebDatabaseContext(Settings.Global.DataRoot);
        Response.Success();

        Response.WriteLine($"# 🔭 Capsules with security.txt ");
        Response.WriteLine("The following are capsules using the \"security.txt\" standard, allowing people to easily contact capsule owners about security issues.");
        Response.WriteLine("=> https://securitytxt.org About Security.txt");
        Response.WriteLine();

        var servers = (
            from securityTxt in db.SecurityTxts
            join favicon in db.Favicons
                on new { securityTxt.Protocol, securityTxt.Domain, securityTxt.Port }
                equals new { favicon.Protocol, favicon.Domain, favicon.Port } into matchingFavicons
            from favicon in matchingFavicons.DefaultIfEmpty()
            orderby securityTxt.Domain
            select new
            {
                SecurityTxt = securityTxt,
                Favicon = favicon == null ? null : favicon.Emoji
            }).ToList();

        Response.WriteLine($"## Capsules with security.txt ({servers.Count()})");

        int count = 0;
        foreach (var server in servers)
        {
            count++;
            var host = server.SecurityTxt;
            var label = $"{count}. {host.Domain}";
            if (server.Favicon != null)
            {
                label = $"{count}. {server.Favicon} {host.Domain}";
            }
            if (host.Port != 1965)
            {
                label += ":" + host.Port;
            }
            Response.WriteLine($"=> {host.Protocol}://{host.Domain}:{host.Port}/.well-known/security.txt {label}");
        }
    }
}
