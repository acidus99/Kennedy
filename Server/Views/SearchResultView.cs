using System;
using System.IO;
using System.Text.RegularExpressions;

using Gemini.Net;
using Kennedy.CrawlData;
using RocketForce;

namespace Kennedy.Server.Views
{
    internal class SearchResultView :AbstractView
    {
        const int resultsInPage = 15;

        public SearchResultView(Request request, Response response, App app)
            : base(request, response, app) { }

        public override void Render()
        {
            var query = SanitizedQuery;
            var options = new SearchOptions(Request.Url, "/search");

            Response.Success();
            Response.WriteLine($"# '{query}' - 🔭 Kennedy Search");
            Response.WriteLine("=> /search New Search");
            Response.WriteLine();
            int baseCounter = options.SearchPage - 1;

            var engine = new FullTextSearchEngine("/var/gemini/crawl-data/");
            var resultCount = engine.GetResultsCount(query);

            if (resultCount > 0)
            {
                var results = engine.DoSearch(query, baseCounter * resultsInPage, resultsInPage);

                int counter = baseCounter * resultsInPage;
                int start = counter + 1;
                foreach (var result in results)
                {
                    counter++;
                    var title = ConstructTitle(result);
                    Response.WriteLine($"=> {result.Url} {counter}. {title}");
                    if (result.IsRecognizedLanguage)
                    {
                        Response.WriteLine($"* {result.LineCount} Lines - {result.FormattedLanguage} - {FormatSize(result.BodySize)} - {FormatDomain(result)}");
                    }
                    else
                    {
                        Response.WriteLine($"* {result.LineCount} Lines - {FormatSize(result.BodySize)} - {FormatDomain(result)}");
                    }

                    Response.WriteLine(">" + FormatSnippet(result.Snippet));
                    if (result.BodySaved)
                    {
                        Response.WriteLine($"=> /delorean?id={result.DBDocID} Cached copy");
                    }
                    Response.WriteLine("");
                }

                Response.WriteLine($"Showing {start} - {counter}  of {resultCount} total results");

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

        private string PageLink(string linkText, int page)
            => $"=> /search/p:{page}/?{Request.Url.RawQuery} {linkText}";


        private string ConstructTitle(FullTextSearchResult result)
        {
            if (result.Title.Trim().Length > 0)
            {
                return result.Title;
            }
            return $"{result.Url.Hostname}{result.Url.Path}";
        }

        private string FormatDomain(FullTextSearchResult result)
        {
            return (result.Favicon.Length > 0) ? $"{result.Favicon} {result.Url.Hostname}" : $"{result.Url.Hostname}";
        }

        private string FormatSize(int bodySize)
        {
            if (bodySize < 1024)
            {
                return $"{bodySize} B";
            }

            return $"{Math.Round(((double)bodySize) / ((double)1024))} KB";
        }

        private string FormatSnippet(string snippet)
        {
            snippet = snippet.Replace("\r", "").Replace("\n", " ").Replace("#", "").Trim();
            //collapse whitespace runs
            return Regex.Replace(snippet, @"\s+", " ");
        }
    }
}
