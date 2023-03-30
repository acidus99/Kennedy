using System;
using System.Linq;
using System.Web;
using Gemini.Net;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.CrawlData;
using Microsoft.EntityFrameworkCore;
using RocketForce;

namespace Kennedy.Server.Views.Archive
{

    /// <summary>
    /// Shows the details about a 
    /// </summary>
    internal class SearchResultsView :AbstractView
    {
        ArchiveDbContext archive = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");

        public SearchResultsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            string query = SanitizedQuery;

            var urls = archive.Urls
                .Where(x => x.FullUrl.Contains(query));

            var count = urls.Count();

             urls = urls.OrderBy(x => x.FullUrl.IndexOf(query))
                .ThenBy(x => x.FullUrl.Length)
                .Take(50);

            Response.Success();
            Response.WriteLine($"# 🏎 DeLorean Time Machine");
            Response.WriteLine();
            Response.Write($"Found {count} urls matching query '{query}'.");

            if (count > 50)
            {
                Response.Write(" Here are the 50 most relevant.");
            }
            Response.WriteLine();

            Response.WriteLine("## Matches");

            int counter = 1;
            foreach (var url in urls)
            {
                Response.WriteLine($"=>{RoutePaths.ViewUrlHistory(url.GeminiUrl)} {counter}. {url.GeminiUrl.NormalizedUrl}");
                counter++;
            }
        }

    }
}
