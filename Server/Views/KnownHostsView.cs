using System.Linq;
using Gemini.Net;
using Kennedy.SearchIndex.Web;
using RocketForce;

namespace Kennedy.Server.Views;

internal class KnownHostsView : AbstractView
{

    public KnownHostsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        Response.Success();

        Response.WriteLine($"# 🔭 Known Gemini Caspules");
        Response.WriteLine();
        Response.WriteLine("The following are capsules that:");
        Response.WriteLine("* Are known to Kennedy.");
        Response.WriteLine("* Resolve to an IP address.");
        Response.WriteLine("* Properly accept TLS connections");
        Response.WriteLine("* Send with a valid Gemini response.");

        using (var db = new WebDatabaseContext(Settings.Global.DataRoot))
        {
            var servers = (
                from document in db.Documents
                join favicon in db.Favicons
                    on new { document.Protocol, document.Domain, document.Port }
                    equals new { favicon.Protocol, favicon.Domain, favicon.Port } into matchingFavicons
                from favicon in matchingFavicons.DefaultIfEmpty()
                where document.StatusCode != GeminiParser.ConnectionErrorStatusCode
                group document by new
                {
                    document.Protocol,
                    document.Domain,
                    document.Port,
                    Favicon = favicon == null ? null : favicon.Emoji
                } into capsuleGroup
                select new
                {
                    capsuleGroup.Key.Protocol,
                    capsuleGroup.Key.Domain,
                    capsuleGroup.Key.Port,
                    capsuleGroup.Key.Favicon,
                    Pages = capsuleGroup.Count(),
                    LastDate = capsuleGroup.Max(d => d.LastTimeUpdated)
                }).ToList();

            Response.WriteLine($"## Known Capsules ({servers.Count()})");

            int counter = 0;
            foreach (var server in servers)
            {
                counter++;
                var label = $"{counter}. ";
                if (server.Favicon != null)
                {
                    label += server.Favicon + " ";
                }
                label += FormatDomain(server.Domain, null);
                if (server.Port != 1965)
                {
                    label += ":" + server.Port;
                }
                label += $" ({server.Pages} URLs. Updated: {server.LastDate})";
                Response.WriteLine($"=> {server.Protocol}://{server.Domain}:{server.Port}/ {label}");
            }
        }
    }
}
