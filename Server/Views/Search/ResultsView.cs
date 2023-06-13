using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Gemini.Net;
using Kennedy.Gemipedia;
using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Models;
using RocketForce;


namespace Kennedy.Server.Views.Search
{
    internal class ResultsView :AbstractView
    {
        const int resultsInPage = 15;

        public ResultsView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app)
        {
            SearchEngine = new SearchDatabase(Settings.Global.DataRoot);
        }

        SearchDatabase SearchEngine;

        ArticleSummary? TopGemipediaHit;
        List<FullTextSearchResult>? SearchResults;
        int ImageHits = 0;

        int ResultCount = 0;

        SearchOptions Options = null!;

        public override void Render()
        {
            var queryParser = new QueryParser();
            UserQuery query = queryParser.Parse(SanitizedQuery);

            Options = new SearchOptions(Request.Url, "/search");

            Response.Success();
            Response.WriteLine($"# '{query}' - 🔭 Kennedy Search");
            Response.WriteLine();

            ResultCount = SearchEngine.GetTextResultsCount(query);
            if (ResultCount > 0)
            {
                RenderResults(query);
            }
            else
            {
                RenderNoResults(query);
            }
        }

        private void RenderNoResults(UserQuery query)
        {
            Response.WriteLine("Sorry, no results for your search.");

            var suggestedQuery = QuerySuggestor.MakeOrQuery(query);

            Response.WriteLine($"=> {RoutePaths.Search(suggestedQuery.RawQuery)} Try searching \"{suggestedQuery}\" instead?");
            Response.WriteLine($"=> {RoutePaths.Search()} New Search");
        }

        private void RenderResults(UserQuery query)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            DoFullQuery(query);
            stopwatch.Stop();
            var queryTime = (int)stopwatch.ElapsedMilliseconds;
            int baseCounter = Options.SearchPage - 1;
            int counter = baseCounter * resultsInPage;
            int start = counter + 1;

            bool shownHeader = false;

            if (TopGemipediaHit != null)
            {
                Response.WriteLine($"=> {Helper.ArticleUrl(TopGemipediaHit)} Gemipedia Article: {TopGemipediaHit.Title}");
                if (TopGemipediaHit.Description.Length > 0)
                {
                    Response.WriteLine($"> {TopGemipediaHit.Description}");
                }
                shownHeader = true;
            }

            if (ImageHits > 0)
            {
                Response.WriteLine($"=> /image-search?{Request.Url.RawQuery} {ImageHits} matches on 🖼 Image Search for {query}");
                shownHeader = true;
            }

            if (shownHeader)
            {
                Response.WriteLine();
            }

            if (SearchResults != null)
            {
                Response.WriteLine($"Showing {FormatCount(start)} - {FormatCount(start + SearchResults.Count - 1)} of {FormatCount(ResultCount)} results");
                foreach (var result in SearchResults)
                {
                    counter++;
                    WriteResultEntry(result, counter);
                }
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
            Response.WriteLine("=> /search 🔍 Another Search");
            Response.WriteLine("=> /image-search 🖼 Image Search");
            Response.WriteLine("=> / Home");
        }

        private void WriteResultEntry(FullTextSearchResult result, int resultNumber)
        {
            var resultTitle = result.Title ?? FormatUrl(result.GeminiUrl);

            // Write link line with meta data.
            Response.Write($"=> {result.Url} {FormatCount(resultNumber)}. {resultTitle} ({result.Mimetype} • ");

            if (result.LineCount != null)
            {
                Response.Write(result.LineCount.Value.ToString());
                Response.Write(" Lines • ");
            }
            Response.Write($"{FormatSize(result.BodySize)})");
            Response.WriteLine();
            if (result.Title != null)
            {
                Response.WriteLine($"* {FormatUrl(result.GeminiUrl)}");
            }
            if (result.DetectedLanguage != null && result.DetectedLanguage != "en")
            {
                Response.WriteLine($"Language: {FormatLanguage(result.DetectedLanguage)}");
            }

            // Write quote line with snippet.
            Response.WriteLine(">" + FormatSnippet(result.Snippet));

            // Write link line to archive/meta data.
            Response.WriteLine($"=> {RoutePaths.ViewUrlInfo(result.GeminiUrl)} ℹ️ More Info / Archived Copy");

            // Write blank line between entries.
            Response.WriteLine("");
        }

        private void QueryGemipedia(string? query)
        {
            if (query != null && Options.SearchPage == 1)
            {
                var client = new WikipediaApiClient();
                TopGemipediaHit = client.TopResultSearch(query);
            }
        }

        private void QueryFullText(UserQuery query)
        {
            int baseCounter = Options.SearchPage - 1;
            SearchResults = SearchEngine.DoTextSearch(query, baseCounter * resultsInPage, resultsInPage);
        }

        private void QueryImageSearch(UserQuery query)
        {
            ImageHits = SearchEngine.GetImageResultsCount(query);
        }

        private void DoFullQuery(UserQuery query)
        {
            Parallel.Invoke(() => QueryGemipedia(query.TermsQuery),
                            () => QueryFullText(query),
                            () => QueryImageSearch(query));
        }

        private string PageLink(string linkText, int page)
            => $"=> /search/p:{page}/?{Request.Url.RawQuery} {linkText}";

        private string FormatSnippet(string snippet)
        {
            snippet = snippet.Replace("\r", "").Replace("\n", " ").Replace("#", "").Trim();
            //collapse whitespace runs
            return Regex.Replace(snippet, @"\s+", " ");
        }
    }
}
