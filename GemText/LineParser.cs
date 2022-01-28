using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace GemiCrawler.GemText
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
