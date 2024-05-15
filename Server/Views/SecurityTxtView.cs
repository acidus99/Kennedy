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
        var db = new WebDatabaseContext(Settings.Global.DataRoot);
        Response.Success();

        Response.WriteLine($"# 🔭 Capsules with security.txt ");
        Response.WriteLine("The following are capsules using the \"security.txt\" standard, allowing people to easily contact capsule owners about security issues.");
        Response.WriteLine("=> https://securitytxt.org About Security.txt");
        Response.WriteLine();

        var servers = db.SecurityTxts.OrderBy(x => x.Domain);

        Response.WriteLine($"## Capsules with security.txt ({servers.Count()})");

        int count = 0;
        foreach (var host in servers)
        {
            count++;
            var label = $"{count}. {host.Domain}";
            if (host.Port != 1965)
            {
                label += ":" + host.Port;
            }
            Response.WriteLine($"=> gemini://{host.Domain}:{host.Port}/.well-known/security.txt {label}");
        }
    }
}