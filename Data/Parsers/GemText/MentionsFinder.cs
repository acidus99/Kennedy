using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

using Kennedy.Data.Parsers.GemText;
using Kennedy.Data.Utils;
using NTextCat.Commons;

namespace Kennedy.Data.Parsers.GemText
{
    public static class MentionsFinder
    {
        private static readonly Regex[] AllowedFormats = new Regex[]
       {
           //after whitespace or a comma.
           new Regex(@"[\s\,][\@\~]([a-zA-Z_][a-zA-Z\d_\-]{2,})\s", RegexOptions.Compiled),
           //start of line
           new Regex(@"^[\@\~]([a-zA-Z_][a-zA-Z\d_\-]{2,})\s", RegexOptions.Compiled),
       };

        public static IEnumerable<string> GetMentions(List<string> lines)
        {
            var mentions = new Bag<string>();
            lines.ForEach(line => mentions.AddRange(GetMentionsForLine(line)));
            return mentions.GetValues();
        }

        /// <summary>
        /// filter line down to where mentions can be
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static string FilterLine(string line)
        {
            if (line.StartsWith("=>"))
            {
                //only search link text to avoid flagging on URL fragments
                return LinkFinder.GetLinkText(line) + " ";
            }
            if (LineParser.IsHeading(line))
            {
                //only search text of heading
                return LineParser.ParseHeading(line).Item2 + " ";
            }
            return line + " ";
        }

        private static IEnumerable<string> GetMentionsForLine(string line)
        {
            var filtered = FilterLine(line);
            var matches = new List<string>();
            foreach (var regex in AllowedFormats)
            {
                matches.AddRange(regex.Matches(filtered).Select(x => Normalize(x.Groups[1].Value)));
            }
            return matches.ToHashSet();
        }

        private static string Normalize(string value)
           => value.ToLower();
    }
}