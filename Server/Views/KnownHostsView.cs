using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;


using Gemini.Net;
using Kennedy.Data;
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
            var webDatabase = new WebDatabase(Settings.Global.DataRoot);

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

            var servers = webDatabase.GetAllCapsules();

            Response.WriteLine($"## Known Capsules ({servers.Count()})");

            int counter = 0;
            foreach (var server in servers)
            {
                counter++;
                var label = $"{counter}. {FormatDomain(server.Domain, server.Emoji)}";
                if(server.Port != 1965)
                {
                    label += ":" + server.Port;
                }
                label += $" ({server.Pages} URLs)";
                Response.WriteLine($"=> {server.Protocol}://{server.Domain}:{server.Port}/ {label}");
            }
        }
    }
}
