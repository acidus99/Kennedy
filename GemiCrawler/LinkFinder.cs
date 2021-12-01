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
            } else if(resp.IsSuccess && resp.MimeType.StartsWith("text/gemini") && resp.ResponseText?.Length > 0)
            {

                var foundLinks =
                            (from line in resp.ResponseText.Split("\n")
                             let match = linkLine.Match(line)
                             where match.Success
                             let gurl = GemiUrl.MakeUrl(request, match.Groups[1].Value)
                             where gurl != null
                             select gurl);
                urls.AddRange(foundLinks);
            }

            return urls;
        }

    }


}
