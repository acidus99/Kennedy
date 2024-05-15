using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Search;
using RocketForce;

namespace Kennedy.Server.Views.Search;

internal class ImageResultsView : AbstractView
{
    const int resultsInPage = 15;

    public ImageResultsView(GeminiRequest request, Response response, GeminiServer app)
        : base(request, response, app) { }

    ISearchDatabase ImageEngine = new SearchDatabase(Settings.Global.DataRoot);

    int ResultCount = 0;
    SearchOptions Options = null!;

    public override void Render()
    {
        var queryParser = new QueryParser();
        UserQuery query = queryParser.Parse(SanitizedQuery);

        Options = new SearchOptions(Request.Url, RoutePaths.ImageSearchRoute);
        Response.Success();

        if (!query.IsValidImageQuery)
        {
            RenderBadQuery(query);
            return;
        }

        Response.WriteLine($"# '{query}' - 🔭 Kennedy Image Search");
        Response.WriteLine();

        ResultCount = ImageEngine.GetImageResultsCount(query);
        if (ResultCount > 0)
        {
            RenderResults(query);
        }
        else
        {
            RenderNoResults(query);
        }
    }

    private void RenderResults(UserQuery query)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        var results = DoQuery(query, Options);
        stopwatch.Stop();

        var queryTime = (int)stopwatch.ElapsedMilliseconds;
        int baseCounter = Options.SearchPage - 1;
        int counter = baseCounter * resultsInPage;
        int start = counter + 1;

        Response.WriteLine($"Showing {FormatCount(start)} - {FormatCount(start + results.Count - 1)} of {FormatCount(ResultCount)} results");

        foreach (var result in results)
        {
            counter++;
            WriteResultEntry(Response, result, counter);
        }

        Response.WriteLine($"Showing {FormatCount(start)} - {FormatCount(counter)}  of {FormatCount(ResultCount)} total results");

        if (Options.SearchPage > 1)
        {
            //show previous link
            Response.WriteLine(PageLink("⬅️ Previous Page", Options.SearchPage - 1));
        }

        if ((baseCounter * resultsInPage) + resultsInPage < ResultCount)
        {
            //show next page
            Response.WriteLine(PageLink("➡️ Next Page", Options.SearchPage + 1));
        }
        Response.WriteLine($"Query time: {queryTime} ms");
        Response.WriteLine();
        Response.WriteLine($"=> {RoutePaths.ImageSearchRoute} 🖼 Another Search");
        Response.WriteLine("=> /search 🔍 Text Search");
        Response.WriteLine("=> / Home");
    }

    private void RenderBadQuery(UserQuery query)
    {
        Response.WriteLine("Sorry, we could not understand the query for a image search.");
        Response.WriteLine($"> {query.RawQuery}");

        Response.WriteLine($"=> {RoutePaths.ImageSearchRoute} 🖼 New Search");
    }


    private void RenderNoResults(UserQuery query)
    {
        Response.WriteLine("Sorry, no image results for your search.");

        var suggestedQuery = QuerySuggestor.MakeOrQuery(query);

        Response.WriteLine($"=> {RoutePaths.ImageSearch(suggestedQuery.RawQuery)} Try searching \"{suggestedQuery}\" instead?");
        Response.WriteLine($"=> {RoutePaths.ImageSearchRoute} 🖼 New Search");
    }

    private void WriteResultEntry(Response resp, ImageSearchResult result, int resultNumber)
    {
        Response.Write($"=> {result.Url} {FormatCount(resultNumber)}. {FormatFilename(result.GeminiUrl)} ({result.Width} x {result.Height} • {result.ImageType}");
        if (!result.IsBodyTruncated)
        {
            Response.Write($" • {FormatSize(result.BodySize)}");
        }
        Response.WriteLine(")");
        Response.WriteLine($"* {FormatUrl(result.GeminiUrl)}");
        Response.WriteLine(">" + FormatSnippet(result.Snippet));
        Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(result.GeminiUrl)} ℹ️ More Info / Archived Copy");
        Response.WriteLine("");
    }

    private List<ImageSearchResult> DoQuery(UserQuery query, SearchOptions options)
    {
        int baseCounter = options.SearchPage - 1;
        return ImageEngine.DoImageSearch(query, baseCounter * resultsInPage, resultsInPage);
    }

    private string PageLink(string linkText, int page)
        => $"=> {RoutePaths.ImageSearchRoute}/p:{page}/?{Request.Url.RawQuery} {linkText}";

    private string FormatSnippet(string snippet)
    {
        snippet = snippet.Replace("\r", "").Replace("\n", " ").Replace("#", "").Trim();
        //collapse whitespace runs
        return Regex.Replace(snippet, @"\s+", " ");
    }

    private string PrepareQuery(string query)
    {
        return query.Replace(".", " ");
    }
}