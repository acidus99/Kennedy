using System;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Data.Parsers;

namespace Kennedy.Crawler.Crawling
{
    /// <summary>
    /// Finds links in a gemini response, including the header as well as the body
    /// </summary>
    public class ResponseLinkFinder : ILinksFinder
    {
        ResponseParser responseParser = new ResponseParser();

        public IEnumerable<FoundLink>? FindLinks(GeminiResponse response)
        {
            var parsedResponse = responseParser.Parse(response);
            return parsedResponse.Links;
        }
    }
}

