using System;
using System.Linq;
using System.IO;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class KnownHostsView :AbstractView
    {

        public KnownHostsView(Request request, Response response, App app)
            : base(request, response, app) { }

        public override void Render()
        {

            var db = (new DocumentIndex(Settings.Global.DataRoot)).GetContext();

            Response.Success();
            /*
             * # 🔭 Kennedy: Search Gemini Space
=> /search New Search
=> /lucky I'm Feeling Lucky */

            Response.WriteLine($"# 🔭 Known Gemini Hosts");
            Response.WriteLine("=> /search New Search");
            Response.WriteLine("=> /lucky I'm Feeling Lucky");
            Response.WriteLine();
            Response.WriteLine("## Known Hosts");

            var domains = db.DocEntries.Select(x=>x.Domain).Distinct().ToList();

            domains.Sort();

            foreach (var domain in domains)
            {
                var port = 1965;
                var fav = GetFavicon(domain, port);
                Response.WriteLine($"=> gemini://{domain}/ {fav}{domain}");
            }

        }

        private string GetFavicon(string host, int port)
        {
            try
            {
                //return File.ReadAllText($"/var/gemini/crawl-data/favicons/{host}@{port}!favicon.txt") + " ";
            }
            catch (Exception) { }
            return "";
        }

    }
}
