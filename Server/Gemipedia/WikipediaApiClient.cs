using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;

using Kennedy.Cache;

namespace Kennedy.Gemipedia
{
    public class WikipediaApiClient
    {
        static DiskCache Cache = new DiskCache();

        WebClient client;

        public WikipediaApiClient()
        {
            client = new WebClient();
            client.Headers.Add(HttpRequestHeader.UserAgent, "GeminiProxy/0.1 (gemini://gemi.dev/; acidus@gemi.dev) gemini-proxy/0.1");
        }

        public ArticleSummary? TopResultSearch(string query)
        {
            return Search(query).FirstOrDefault();
        }

        /// <summary>
        /// Performance a search using the "rest.php/v1/search/page" endpoint
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public List<ArticleSummary> Search(string query)
        {
            var url = $"https://en.wikipedia.org/w/rest.php/v1/search/page?q={WebUtility.UrlEncode(query)}&limit=3";
            return ResponseParser.ParseSearchResponse(FetchString(url));
        }

        //Downloads a string, if its not already cached
        private string FetchString(string url)
        {
            //first check the cache
            var contents = Cache.GetAsString(url);
            if(contents != null)
            {
                return contents;
            }
            //fetch it
            contents = client.DownloadString(url);
            //cache it
            Cache.Set(url, contents);
            return contents;
        }
    }
}