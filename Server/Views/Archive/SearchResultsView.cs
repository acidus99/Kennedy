using System.Linq;
using Kennedy.Archive.Db;
using RocketForce;

namespace Kennedy.Server.Views.Archive;

/// <summary>
/// Shows the details about a 
/// </summary>
internal class SearchResultsView : AbstractView
{
    const int MaxResults = 100;

    ArchiveDbContext archive = new ArchiveDbContext(Settings.Global.DataRoot + "archive.db");

    public SearchResultsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    public override void Render()
    {
        string query = SanitizedQuery;

        var urls = archive.Urls
            .Where(x => x.FullUrl.Contains(query) && x.IsPublic);

        var count = urls.Count();

        urls = urls.OrderBy(x => x.FullUrl.IndexOf(query))
           .ThenBy(x => x.FullUrl.Length)
           .ThenBy(x => x.FullUrl)
           .Take(MaxResults);

        Response.Success();
        Response.WriteLine($"# 🏎 DeLorean Time Machine");
        Response.WriteLine();
        Response.Write($"Found {FormatCount(count)} urls matching query '{query}'.");

        if (count > MaxResults)
        {
            Response.Write($" Here are the {MaxResults} most relevant.");
        }
        Response.WriteLine();

        Response.WriteLine("## Matches");

        int counter = 1;
        foreach (var url in urls)
        {
            Response.WriteLine($"=>{RoutePaths.ViewUrlUniqueHistory(url.GeminiUrl)} {counter}. {url.GeminiUrl.NormalizedUrl}");
            counter++;
        }
    }
}