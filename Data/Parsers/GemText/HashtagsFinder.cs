using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

using Kennedy.Data.Utils;

namespace Kennedy.Data.Parsers.GemText
{
    public static class HashtagsFinder
    {
        private static readonly Regex HashtagFormat = new Regex(@"[\,\s]#([a-zA-Z0-9][a-zA-Z0-9_\-]+)", RegexOptions.Compiled);

        private static readonly Regex[] ExcludedFormats = new Regex[]
        {
           //only numbers or number ranges
           new Regex(@"^[\d\-]+$", RegexOptions.Compiled),
           //CSS hex color 3 or 6
           new Regex(@"^[a-fA-F0-9]{3}$", RegexOptions.Compiled),
           new Regex(@"^[a-fA-F0-9]{6}$", RegexOptions.Compiled),
        };


        public static IEnumerable<string> GetHashtags(List<string> lines)
        {
            var hashtags = new Bag<string>();
            lines.ForEach(line => hashtags.AddRange(GetTagsForLine(line)));
            return hashtags.GetValues();
        }

        public static IEnumerable<string> GetTagsForLine(string line)
        {
            line = FilterLine(line);
            return HashtagFormat.Matches(line)
                           .Where(x => IsGoodHashtag(x.Groups[1].Value))
                           .Select(x => Normalize(x.Groups[1].Value));
        }

        /// <summary>
        /// filter line down to where hashtags can be
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static string FilterLine(string line)
        {
            if (line.StartsWith("=>"))
            {
                //only search link text to avoid flagging on URL fragments
                return LinkFinder.GetLinkText(line);
            }
            if (LineParser.IsHeading(line))
            {
                //only search text of heading
                return LineParser.ParseHeading(line).Item2;
            }
            return line;
        }

        /// <summary>
        /// Ensure hashtag doesn't match excluded format regexs
        /// </summary>
        /// <param name="hashtag"></param>
        /// <returns></returns>
        private static bool IsGoodHashtag(string hashtag)
        {
            foreach (var regex in ExcludedFormats)
            {
                if (regex.IsMatch(hashtag))
                {
                    return false;
                }
            }
            return true;
        }

        private static string Normalize(string value)
            => value.ToLower();
    }
}