using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Gemi.Net;
using System.Linq;

namespace GemiCrawler.GemText
{
    /// <summary>
    /// Finds hyperlinks in Gemini Text Documents
    /// </summary>
    public static class LinkFinder
    {
        static readonly Regex linkLine = new Regex(@"^=>\s+([^\s]+)\s*(.*)", RegexOptions.Compiled);

        public static List<FoundLink> ExtractLinks(GemiResponse resp)
        {
            var links = new List<FoundLink>();

            if(resp.IsRedirect)
            {
                var foundLink = Create(resp.RequestUrl, resp.Meta);
                if(foundLink != null)
                {
                    links.Add(foundLink);
                }
            }
            else if(resp.IsSuccess && resp.HasBody && resp.MimeType.StartsWith("text/gemini"))
            {
                var foundLinks =
                            (from line in resp.BodyText.Split("\n")
                             let match = linkLine.Match(line)
                             where match.Success
                             let link = Create(resp.RequestUrl, match)
                             where link != null
                             select link);
                links.AddRange(foundLinks);
            }

            return links;
        }

        private static FoundLink Create(GemiUrl pageUrl, Match match)
            => Create(pageUrl, match.Groups[1].Value, getLinkText(match));

        private static FoundLink Create(GemiUrl pageUrl, string foundUrl, string linkText = "")
        {
            var newUrl = GemiUrl.MakeUrl(pageUrl, foundUrl);
            //ignore anything that doesn't resolve properly, or isn't to a gemini:// URL
            if (newUrl == null)
            {
                return null;
            }
            return new FoundLink
            {
                Url = newUrl,
                IsExternal = (newUrl.Authority != pageUrl.Authority),
                LinkText = linkText
            };
        }

        /// <summary>
        /// gives us the text, if any, used with this link
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        private static string getLinkText(Match match)
            => (match.Groups.Count > 2) ? match.Groups[2].Value : "";

    }


}
