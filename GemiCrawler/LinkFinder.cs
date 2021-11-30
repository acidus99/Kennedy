using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Gemi.Net;
using System.Linq;

namespace GemiCrawler
{
    /// <summary>
    /// Finds hyperlinks in Gemini Text Documents
    /// </summary>
    public static class LinkFinder
    {
        static readonly Regex linkLine = new Regex(@"^=>\s([^\s]+)\s*(.*)", RegexOptions.Compiled);

        public static List<GemiUrl> ExtractUrls(GemiUrl request, GemiResponse resp)
        {
            List<GemiUrl> urls = new List<GemiUrl>();

            if(resp.IsRedirect)
            {
                urls.Add(resp.Redirect);
            } else if(resp.IsSuccess && resp.MimeType.StartsWith("text/gemini"))
            {

                var foundLinks =
                            (from line in resp.ResponseText.Split("\n")
                             let match = linkLine.Match(line)
                             where match.Success
                             where IsValidGemiUrl(match.Groups[1].Value)
                             select new
                             {
                                 url = match.Groups[1].Value
                             })
                             .AsEnumerable().Select(x => GemiUrl.MakeUrl(request, x.url));


                urls.AddRange(foundLinks);
            }


            return urls;
        }


        private static bool IsValidGemiUrl(string foundUrl)
            => !foundUrl.Contains("://") || foundUrl.StartsWith("gemini://");

        

    }


}
