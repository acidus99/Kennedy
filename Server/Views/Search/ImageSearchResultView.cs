using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

using Kennedy.CrawlData.Search;
using RocketForce;
using System.Diagnostics;

namespace Kennedy.Server.Views.Search
{
    internal class ImageSearchResultsView :AbstractView
    {
        const int resultsInPage = 15;

        public ImageSearchResultsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        List<ImageSearchResult> SearchResults = null;
        ImageSearchEngine ImageEngine;
        SearchOptions Options;

        public override void Render()
        {
            var query = PrepareQuery(SanitizedQuery);
            Options = new SearchOptions(Request.Url, "/image-search");
            ImageEngine = new ImageSearchEngine(Settings.Global.DataRoot);

            Response.Success();
            Response.WriteLine($"# '{query}' - 🔭 Kennedy Image Search");
            Response.WriteLine("=> /image-search 🖼 New Image Search ");
            Response.WriteLine();
            
            var resultCount = ImageEngine.GetResultsCount(query);
            if (resultCount > 0)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                DoQuery(query);
                stopwatch.Stop();
                var queryTime = (int)stopwatch.ElapsedMilliseconds;
                int baseCounter = Options.SearchPage - 1;
                int counter = baseCounter * resultsInPage;
                int start = counter + 1;

                foreach (var result in SearchResults)
                {
                    counter++;
                    WriteResultEntry(Response, result, counter);
                }

                Response.WriteLine($"Showing {start} - {counter}  of {resultCount} total results");

                if (Options.SearchPage > 1)
                {
                    //show previous link
                    Response.WriteLine(PageLink("⬅️ Previous Page", Options.SearchPage - 1));
                }

                if ((baseCounter * resultsInPage) + resultsInPage < resultCount)
                {
                    //show next page
                    Response.WriteLine(PageLink("➡️ Next Page", Options.SearchPage + 1));
                }
                Response.WriteLine($"Query time: {queryTime} ms");
                Response.WriteLine();
                Response.WriteLine("=> /search New Search");
                Response.WriteLine("=> /lucky I'm Feeling Lucky");
                Response.WriteLine("=> / Home");
            }
            else
            {
                Response.WriteLine("## Oh Snap! No Results for your query.");
            }
        }

        private void WriteResultEntry(Response resp, ImageSearchResult result, int resultNumber)
        {
            Response.WriteLine($"=> {result.Url} {resultNumber}. {result.ImageType} • {result.Width} x {result.Height} • {result.Url.Path}");
            Response.WriteLine($"=> /page-info?id={result.DBDocID} {FormatSize(result.BodySize)} • {FormatDomain(result.Url.Hostname, result.Favicon)} • More info...");
            Response.WriteLine(">" + FormatSnippet(result.Snippet));
            Response.WriteLine("");
        }

        private void DoQuery(string query)
        {
            int baseCounter = Options.SearchPage - 1;
            SearchResults = ImageEngine.DoSearch(query, baseCounter * resultsInPage, resultsInPage);
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
