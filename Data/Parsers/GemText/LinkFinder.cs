using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Data.Parsers.GemText
{
    /// <summary>
    /// Finds hyperlinks in Gemini Text Documents
    /// </summary>
    public static class LinkFinder
    {
        static readonly Regex linkLine = new Regex(@"^=>\s*([^\s]+)\s*(.*)", RegexOptions.Compiled);

        /// <summary>
        /// Returns the link text from a link line. If not a link line, or no link text is present, returns ""
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static string GetLinkText(string line)
        {
            if (!line.StartsWith("=> "))
            {
                return "";
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            //part[0] is "=>"
            //part[1] is the url
            //part[2..n] are the optional link text if any 

            if (parts.Length > 2)
            {
                return string.Join(' ', parts.Skip(2));
            }
            return "";
        }

        public static IEnumerable<FoundLink> GetLinks(GeminiUrl requestUrl, IEnumerable<string> bodyLines)
        {
            List<FoundLink> ret = new List<FoundLink>();
            foreach(string line in bodyLines)
            {
                if(!line.StartsWith("=> "))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                //part[0] is "=>"
                //part[1] is the url
                //part[2..n] are the optional link text if any 

                if(parts.Length < 2)
                {
                    //malformed line. doesn't have a URL field
                    continue;
                }

                var url = parts[1];
                //sanity check, if its a fully qualified URL, ensure its a gemini URL. otherwise, we really don't care
                if (url.Contains("://") && !url.StartsWith("gemini://"))
                {
                    continue;
                }

                var newUrl = GeminiUrl.MakeUrl(requestUrl, url);
                //ignore anything that doesn't resolve properly, or isn't to a gemini:// URL
                if (newUrl != null)
                {
                    string linkText = "";
                    //We have a link we care about, so reassemble the link text if any
                    if(parts.Length > 2)
                    {
                        linkText = string.Join(' ', parts.Skip(2));
                    }
                    ret.Add(new FoundLink
                    {
                        Url = newUrl,
                        IsExternal = (newUrl.Authority != requestUrl.Authority),
                        LinkText = linkText
                    });
                }
            }
            return ret;
        }
    }
}