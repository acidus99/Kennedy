using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.SearchIndex.Web;
using Kennedy.SearchIndex.Models;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class KnownHostsView :AbstractView
    {

        public KnownHostsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            var db = new WebDatabaseContext(Settings.Global.DataRoot);

            Response.Success();
            /*
             * # 🔭 Kennedy: Search Gemini Space
=> /search New Search
=> /lucky I'm Feeling Lucky */

            Response.WriteLine($"# 🔭 Known Gemini Caspules");
            Response.WriteLine("=> /search New Search");
            Response.WriteLine("=> /lucky I'm Feeling Lucky");
            Response.WriteLine();
            Response.WriteLine("The following are capsules are known to Kennedy and are reachable.");


            var knownServers = db.Servers.Where(x => x.IsReachable).OrderBy(x => x.Domain).Select(x => new
            {
                Domain = x.Domain,
                Port = x.Port,
                Favicon = !string.IsNullOrEmpty(x.FaviconTxt) ? x.FaviconTxt : ""
            }) ;

            Response.WriteLine($"## Known Capsules ({knownServers.Count()})");

            foreach (var server in knownServers)
            {
                var label = FormatDomain(server.Domain, server.Favicon);
                if(server.Port != 1965)
                {
                    label += ":" + server.Port;
                }
                Response.WriteLine($"=> gemini://{server.Domain}:{server.Port}/ {label}");
            }
        }
    }
}
