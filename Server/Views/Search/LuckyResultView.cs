using Kennedy.Search.Models;
using Kennedy.Search.Query;
using Kennedy.Search.Services;
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

        ISearchService engine = new SqliteSearchService(Settings.Global.SearchDbFile);
        var results = engine.SearchText(query, 0, 1);
        if (results.Count > 0)
        {
            Response.Redirect(results[0].Url);
            return;
        }

        Response.Success();
        Response.WriteLine($"# '{query.RawQuery}' - 🔭 Kennedy Search");
        Response.WriteLine();
        Response.WriteLine("## Oh Snap! No Results for your query.");
    }
}
