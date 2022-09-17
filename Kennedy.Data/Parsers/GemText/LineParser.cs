using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Kennedy.Data.Parsers.GemText
{
    //Extracts
    public static class LineParser
    {
        static readonly Regex headingRegex = new Regex(@"^(#+)\s*(.+)", RegexOptions.Compiled);

        public static IEnumerable<string> RemovePreformatted(string bodyText)
        {
            var ret = new List<string>();
            bool inPre = false;
            //not sure how to make this linq since I'm flip/flopping state
            foreach(var line in bodyText.Split("\n"))
            { 
                if(line.StartsWith("```"))
                {
                    inPre = !inPre;
                } else if(!inPre)
                {
                    ret.Add(line);
                }
            }
            return ret;
        }

        /// <summary>
        /// gets rid of preformatted text, and the hyperlink part of any link lines
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string FilterBody(string text)
        {
            var sb = new StringBuilder();
            foreach (string line in LineParser.RemovePreformatted(text))
            {
                if (line.StartsWith("=>"))
                {
                    sb.AppendLine(LinkFinder.GetLinkText(line));
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            return sb.ToString();
        }

        public static bool IsHeading(string line)
            => headingRegex.IsMatch(line);

        public static Tuple<int, string> ParseHeading(string line)
        {
            Match match = headingRegex.Match(line);
            return new Tuple<int, string>(getCapture(match, 1).Length, getCapture(match, 2));
        }

        /// <summary>
        /// gives us the text from a group, or "" if any, used with this link
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        private static string getCapture(Match match, int group)
            => (match.Groups.Count > group) ? match.Groups[group].Value : "";

    }
}
