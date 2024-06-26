﻿using System.Linq;
using System.Net;
using Kennedy.Cache;

namespace Kennedy.Gemipedia;

public class WikipediaApiClient
{
    static DiskCache Cache = new DiskCache();

    WebClient client;

    public WikipediaApiClient()
    {
        client = new WebClient();
        client.Headers.Add(HttpRequestHeader.UserAgent, "GeminiProxy/0.1 (gemini://gemi.dev/; acidus@gemi.dev) gemini-proxy/0.1");
    }

    /// <summary>
    /// Searchs Wikipedia for a query and returns the top article, if any
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public ArticleSummary? TopResultSearch(string query)
    {
        var url = $"https://en.wikipedia.org/w/rest.php/v1/search/page?q={WebUtility.UrlEncode(query)}&limit=3";

        var apiResponse = FetchString(url);
        if (apiResponse.Length > 0)
        {
            return ApiResponseParser.ParseSearchResponse(apiResponse).FirstOrDefault();
        }
        return null;
    }

    //Downloads a string, if its not already cached
    private string FetchString(string url)
    {
        //first check the cache
        var contents = Cache.GetAsString(url);
        if (contents != null)
        {
            return contents;
        }
        //fetch it
        try
        {
            contents = client.DownloadString(url);
            //cache it
            Cache.Set(url, contents);
            return contents;
        }
        catch (WebException)
        {
        }
        return "";
    }
}