using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.SearchIndex.Web;
using Kennedy.SearchIndex.Models;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class SecurityTxtView :AbstractView
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

            var knownHosts = db.Domains.Where(x => x.IsReachable && x.HasSecurityTxt).OrderBy(x => x.DomainName).Select(x => new
            {
                Hostname = x.DomainName,
                Port = x.Port,
                Favicon = !string.IsNullOrEmpty(x.FaviconTxt) ? x.FaviconTxt : ""
            }) ;

            Response.WriteLine($"## Capsules with security.txt ({knownHosts.Count()})");

            int counter = 0;

            foreach (var host in knownHosts)
            {
                counter++;
                var label = $"{counter}. {host.Favicon}{host.Hostname}";
                if(host.Port != 1965)
                {
                    label += ":" + host.Port;
                }
                Response.WriteLine($"=> gemini://{host.Hostname}:{host.Port}/.well-known/security.txt {label}");
            }
        }
    }
}
