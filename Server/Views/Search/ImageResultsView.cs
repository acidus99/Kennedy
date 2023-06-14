using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Models;
using RocketForce;
using System.Diagnostics;

namespace Kennedy.Server.Views.Search
{
    internal class ImageResultsView :AbstractView
    {
        const int resultsInPage = 15;

        public ImageResultsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        ISearchDatabase ImageEngine = new SearchDatabase(Settings.Global.DataRoot);

        public override void Render()
        {
            var queryParser = new QueryParser();
            UserQuery query = queryParser.Parse(SanitizedQuery);

            var options = new SearchOptions(Request.Url, "/image-search");

            Response.Success();
            Response.WriteLine($"# '{query}' - 🔭 Kennedy Image Search");
            Response.WriteLine();
            
            var resultCount = ImageEngine.GetImageResultsCount(query);
            if (resultCount > 0)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var results = DoQuery(query, options);
                stopwatch.Stop();

                var queryTime = (int)stopwatch.ElapsedMilliseconds;
                int baseCounter = options.SearchPage - 1;
                int counter = baseCounter * resultsInPage;
                int start = counter + 1;

                Response.WriteLine($"Showing {FormatCount(start)} - {FormatCount(start + results.Count - 1)} of {FormatCount(resultCount)} results");

                foreach (var result in results)
                {
                    counter++;
                    WriteResultEntry(Response, result, counter);
                }

                Response.WriteLine($"Showing {FormatCount(start)} - {FormatCount(counter)}  of {FormatCount(resultCount)} total results");

                if (options.SearchPage > 1)
                {
                    //show previous link
                    Response.WriteLine(PageLink("⬅️ Previous Page", options.SearchPage - 1));
                }

                if ((baseCounter * resultsInPage) + resultsInPage < resultCount)
                {
                    //show next page
                    Response.WriteLine(PageLink("➡️ Next Page", options.SearchPage + 1));
                }
                Response.WriteLine($"Query time: {queryTime} ms");
                Response.WriteLine();
                Response.WriteLine("=> /image-search 🖼 Another Search");
                Response.WriteLine("=> /search 🔍 Text Search");
                Response.WriteLine("=> / Home");
            }
            else
            {
                Response.WriteLine("## Oh Snap! No Results for your query.");
            }
        }

        private void WriteResultEntry(Response resp, ImageSearchResult result, int resultNumber)
        {
            Response.WriteLine($"=> {result.Url} {FormatCount(resultNumber)}. {FormatFilename(result.GeminiUrl)} ({result.Width} x {result.Height} • {result.ImageType} • {FormatSize(result.BodySize)})");
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
            => $"=> /image-search/p:{page}/?{Request.Url.RawQuery} {linkText}";

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
}
