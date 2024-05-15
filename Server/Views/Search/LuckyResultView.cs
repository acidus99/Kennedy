using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Search;
using RocketForce;

namespace Kennedy.Server.Views.Search;

internal class LuckyResultsView : AbstractView
{
    public LuckyResultsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        var queryParser = new QueryParser();
        UserQuery query = queryParser.Parse(SanitizedQuery);

        var engine = new SearchDatabase(Settings.Global.DataRoot);
        var results = engine.DoTextSearch(query, 0, 1);
        if (results.Count > 0)
        {
            Response.Redirect(results[0].Url);
            return;
        }
        Response.Success();
        Response.WriteLine($"# '{query}' - 🔭 Kennedy Search");
        Response.WriteLine();
        Response.WriteLine("## Oh Snap! No Results for your query.");
    }
}