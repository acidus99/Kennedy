using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class SecurityTxtView :AbstractView
    {

        public SecurityTxtView(Request request, Response response, App app)
            : base(request, response, app) { }

        public override void Render()
        {

            var db = (new DocumentIndex(Settings.Global.DataRoot)).GetContext();

            Response.Success();
            /*
             * # 🔭 Kennedy: Search Gemini Space
=> /search New Search
=> /lucky I'm Feeling Lucky */

            Response.WriteLine($"# 🔭 Capsules with security.txt ");
            Response.WriteLine("The following are capsules using the \"security.txt\" standard, allowing people to easily contact capsule owners about security issues.");
            Response.WriteLine("=> https://securitytxt.org About Security.txt");
            Response.WriteLine();
            Response.WriteLine("## Capsules with security.txt");

            var knownHosts = db.DomainEntries.Where(x => x.IsReachable && x.HasSecurityTxt).OrderBy(x => x.Domain).Select(x => new
            {
                Hostname = x.Domain,
                Port = x.Port,
                Favicon = !string.IsNullOrEmpty(x.FaviconTxt) ? x.FaviconTxt : ""
            }) ;

            foreach (var host in knownHosts)
            {
                var label = $"{host.Favicon}{host.Hostname}";
                if(host.Port != 1965)
                {
                    label += ":" + host.Port;
                }
                Response.WriteLine($"=> gemini://{host.Hostname}:{host.Port}/.well-known/security.txt {label}");
            }
        }
    }
}
