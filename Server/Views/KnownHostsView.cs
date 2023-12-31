using Gemini.Net;
using Kennedy.SearchIndex.Web;
using RocketForce;
using System.Linq;

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
            var servers = db.Documents
                //.Include(d => d.Favicon)
                .Where(x => x.StatusCode != GeminiParser.ConnectionErrorStatusCode)
                .GroupBy(d => new { d.Protocol, d.Domain, d.Port })
                .Select(g => new
                {
                    g.Key.Protocol,
                    g.Key.Domain,
                    g.Key.Port,
                    //g.First().Favicon,
                    Pages = g.Count()
                });

            Response.WriteLine($"## Known Capsules ({servers.Count()})");

            int counter = 0;
            foreach (var server in servers)
            {
                counter++;
                var label = $"{counter}. {FormatDomain(server.Domain, null)}";
                if (server.Port != 1965)
                {
                    label += ":" + server.Port;
                }
                label += $" ({server.Pages} URLs)";
                Response.WriteLine($"=> {server.Protocol}://{server.Domain}:{server.Port}/ {label}");
            }
        }
    }
}
