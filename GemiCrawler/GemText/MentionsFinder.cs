using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using GemiCrawler.DocumentIndex.Db;
using GemiCrawler.DocumentStore;
using GemiCrawler.Utils;
using GemiCrawler.GemText;

namespace GemiCrawler.GemText
{
    public static class MentionsFinder
    {
        private static readonly Regex[] AllowedFormats = new Regex[]
       {
           //after whitespace or a comma.
           new Regex(@"[\s\,][\@\~]([a-zA-Z_][a-zA-Z\d_\-]{2,})", RegexOptions.Compiled),
           //start of line
           new Regex(@"^[\@\~]([a-zA-Z_][a-zA-Z\d_\-]{2,})", RegexOptions.Compiled),
       };

        public static IEnumerable<string> GetMentions(string bodyText)
        {
            var mentions = new Bag<string>();
            foreach( var line in LineParser.RemovePreformatted(bodyText))
            {
                var m = GetMentionsForLine(line).ToList();
                if (m.Count > 0)
                {
                    mentions.AddRange(m);
                }
            }
            return mentions.GetValues();
        }

        /// <summary>
        /// filter line down to where hashtags can be
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static string FilterLine(string line)
        {
            if(line.StartsWith("=>"))
            {
                //only search link text to avoid flagging on URL fragments
                return Regex.Replace(LinkFinder.GetLinkText(line), @"[a-z]{4,}\:\/\/[^\s]+", " ");
            }
            if(LineParser.IsHeading(line))
            {
                //only search text of heading
                return LineParser.ParseHeading(line).Item2;
            }
            return line;
        }

        private static IEnumerable<string> GetMentionsForLine(string line)
        {
            var filtered = FilterLine(line);
            var matches = new List<string>();
            foreach(var regex in AllowedFormats)
            {
                matches.AddRange(regex.Matches(filtered).Select(x => x.Groups[1].Value));
            }
            return matches.ToHashSet();
        }
    }
}