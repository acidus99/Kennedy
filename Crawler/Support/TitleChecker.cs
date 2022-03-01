using System;
using Gemini.Net;
using Kennedy.Crawler.GemText;

namespace Kennedy.Crawler.Support
{
    public static class TitleChecker
    {
        public static string GetTitle(string url)
        {
            try
            {
                var requestor = new GeminiRequestor();
                var resp = requestor.Request(url);
                var title = TitleFinder.ExtractTitle(resp);

                Console.WriteLine($"Title: \"{title}\"");
                return title;

            }catch(Exception)
            {
                return "==ERROR==";
            }
        }
    }
}
