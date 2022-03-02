using System;
using System.IO;
using System.Text.RegularExpressions;

using Kennedy.CrawlData;
using RocketForce;
using System.Diagnostics;

namespace Kennedy.Server.Views
{
    internal class SearchResultView :AbstractView
    {
        const int resultsInPage = 15;

        public SearchResultView(Request request, Response response, App app)
            : base(request, response, app) { }

        public override void Render()
        {
            var query = PrepareQuery(SanitizedQuery);
            var options = new SearchOptions(Request.Url, "/search");

            Response.Success();
            Response.WriteLine($"# '{query}' - 🔭 Kennedy Search");
            Response.WriteLine("=> /search New Search");
            Response.WriteLine();
            int baseCounter = options.SearchPage - 1;

            bool usePopRank = (options.Algorithm == 1);

            var engine = new FullTextSearchEngine("/var/gemini/crawl-data/");
            var resultCount = engine.GetResultsCount(query);
            
            if (resultCount > 0)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var results = engine.DoSearch(query, baseCounter * resultsInPage, resultsInPage, usePopRank);
                stopwatch.Stop();
                var queryTime = (int)stopwatch.ElapsedMilliseconds;
                int counter = baseCounter * resultsInPage;
                int start = counter + 1;
                foreach (var result in results)
                {
                    counter++;
                    WriteResultEntry(Response, result, counter);
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

        private void WriteResultEntry(Response resp, FullTextSearchResult result, int resultNumber)
        {

            Response.WriteLine($"=> {result.Url} {resultNumber}. {FormatPageTitle(result.Url, result.Title)}");
            Response.Write($"=> /page-info?id={result.DBDocID} {result.LineCount} Lines • ");

            var language = FormatLanguage(result.Language);

            if (language.Length > 0)
            {
                Response.Write($"{language} • ");
            }
            Response.Write($"{FormatSize(result.BodySize)} • {FormatDomain(result.Url.Hostname, result.Favicon)}");
            //if(result.ExternalInboundLinks > 0)
            //{
            //    Response.Write($" • {result.ExternalInboundLinks} inbound links");
            //}
            //if (result.BodySaved)
            //{
            //    Response.Write(" • Cached Copy");
            //    //Response.WriteLine($"=> /cached?id={result.DBDocID} Cached copy");
            //}
            Response.Write("\n");
            Response.WriteLine(">" + FormatSnippet(result.Snippet));

            Response.WriteLine("");

        }

        private string PageLink(string linkText, int page)
            => $"=> /search/p:{page}/?{Request.Url.RawQuery} {linkText}";

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
