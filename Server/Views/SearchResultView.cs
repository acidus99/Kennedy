using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

using Kennedy.CrawlData.Search;
using RocketForce;
using System.Diagnostics;
using Kennedy.Gemipedia;

namespace Kennedy.Server.Views
{
    internal class SearchResultView :AbstractView
    {
        const int resultsInPage = 15;

        public SearchResultView(Request request, Response response, App app)
            : base(request, response, app) { }

        ArticleSummary TopGemipediaHit = null;
        List<FullTextSearchResult> SearchResults = null;
        FullTextSearchEngine FullTextEngine;

        SearchOptions Options;

        public override void Render()
        {
            var query = PrepareQuery(SanitizedQuery);
            Options = new SearchOptions(Request.Url, "/search");
            FullTextEngine = new FullTextSearchEngine(Settings.Global.DataRoot);

            Response.Success();
            Response.WriteLine($"# '{query}' - 🔭 Kennedy Search");
            Response.WriteLine("=> /search New Search");
            Response.WriteLine();
            
            var resultCount = FullTextEngine.GetResultsCount(query);
            if (resultCount > 0)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                DoFullQuery(query);
                stopwatch.Stop();
                var queryTime = (int)stopwatch.ElapsedMilliseconds;
                int baseCounter = Options.SearchPage - 1;
                int counter = baseCounter * resultsInPage;
                int start = counter + 1;

                if(TopGemipediaHit != null)
                {
                    Response.WriteLine($"=> {Helper.ArticleUrl(TopGemipediaHit.Title)} 📖 Top Gemipedia Article: {TopGemipediaHit.Title}");
                    if (!string.IsNullOrEmpty(TopGemipediaHit.ThumbnailUrl))
                    {
                        Response.WriteLine($"=> {Helper.MediaProxyUrl(TopGemipediaHit.ThumbnailUrl)} Article Image: {TopGemipediaHit.Title}");
                    }

                    if (TopGemipediaHit.Description.Length > 0)
                    {
                        Response.WriteLine($"> {TopGemipediaHit.Description}");
                    }
                    Response.WriteLine($"=> {Helper.SearchUrl(TopGemipediaHit.Title)} 📚 Other Gemipedia Articles that mention '{TopGemipediaHit.Title}'");
                    Response.WriteLine();
                }

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

        private void QueryGemipedia(string query)
        {
            if (Options.SearchPage == 1)
            {
                var client = new WikipediaApiClient();
                TopGemipediaHit = client.TopResultSearch(query);
            }
        }

        private void QueryFullText(string query)
        {
            bool usePopRank = (Options.Algorithm == 1);
            int baseCounter = Options.SearchPage - 1;
            SearchResults = FullTextEngine.DoSearch(query, baseCounter * resultsInPage, resultsInPage, usePopRank);
        }

        private void DoFullQuery(string query)
        {
            Parallel.Invoke(() => QueryGemipedia(query),
                            () => QueryFullText(query));
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
