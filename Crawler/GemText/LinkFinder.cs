using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Gemini.Net;

namespace Kennedy.Crawler.GemText
{
    /// <summary>
    /// Finds hyperlinks in Gemini Text Documents
    /// </summary>
    public static class LinkFinder
    {
        static readonly Regex linkLine = new Regex(@"^=>\s*([^\s]+)\s*(.*)", RegexOptions.Compiled);

        public static List<FoundLink> ExtractLinks(GeminiResponse resp)
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
                links.AddRange(ExtractBodyLinks(resp.RequestUrl, resp.BodyText));
            }

            return links;
        }

        /// <summary>
        /// Returns the link text from a link line. If not a link line, or no link text is present, returns ""
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static string GetLinkText(string line)
            => getLinkText(linkLine.Match(line));

        public static IEnumerable<FoundLink> ExtractBodyLinks(GeminiUrl requestUrl, string bodyText)
        {
            var foundLinks =
                            (from line in bodyText.Split("\n")
                             let match = linkLine.Match(line)
                             where match.Success
                             let link = Create(requestUrl, match)
                             where link != null
                             select link);
            return foundLinks;
        }

        private static FoundLink Create(GeminiUrl pageUrl, Match match)
            => Create(pageUrl, match.Groups[1].Value, getLinkText(match));

        public static FoundLink Create(GeminiUrl pageUrl, string foundUrl, string linkText = "")
        {
            var newUrl = GeminiUrl.MakeUrl(pageUrl, foundUrl);
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