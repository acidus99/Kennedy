using System;
using System.IO;

using Gemini.Net;
using Kennedy.SearchIndex.Search;
using RocketForce;

namespace Kennedy.Server.Views.Search
{
    internal class LuckyResultsView :AbstractView
    {
        public LuckyResultsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {
            string query = SanitizedQuery;
            var engine = new FullTextSearchEngine(Settings.Global.DataRoot);
            var results = engine.DoSearch(query, 0, 1);
            if (results.Count > 0)
            {
                Response.Redirect(results[0].Url.NormalizedUrl);
                return;
            }
            Response.Success();
            Response.WriteLine($"# '{query}' - 🔭 Kennedy Search");
            Response.WriteLine();
            Response.WriteLine("## Oh Snap! No Results for your query.");
        }

    }
}
