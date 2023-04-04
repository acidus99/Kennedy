using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.SearchIndex;
using Kennedy.SearchIndex.Db;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class KnownHostsView :AbstractView
    {

        public KnownHostsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {

            var db = (new DocumentIndex(Settings.Global.DataRoot)).GetContext();

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


            var knownHosts = db.Domains.Where(x => x.IsReachable).OrderBy(x => x.Domain).Select(x => new
            {
                Hostname = x.Domain,
                Port = x.Port,
                Favicon = !string.IsNullOrEmpty(x.FaviconTxt) ? x.FaviconTxt : ""
            }) ;

            Response.WriteLine($"## Known Capsules ({knownHosts.Count()})");

            foreach (var host in knownHosts)
            {
                var label = FormatDomain(host.Hostname, host.Favicon);
                if(host.Port != 1965)
                {
                    label += ":" + host.Port;
                }
                Response.WriteLine($"=> gemini://{host.Hostname}:{host.Port}/ {label}");
            }
        }
    }
}
