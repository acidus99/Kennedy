using System;
using System.Text;
using System.Text.RegularExpressions;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
{
    public class GemTextResponseParser : AbstractTextParser
    {
        static readonly Regex Iso8601Date = new Regex(@"^\d{4}\-[01]\d\-[0123]\d", RegexOptions.Compiled);

        LanguageDetector languageDetector = new LanguageDetector();

        public override bool CanParse(GeminiResponse resp, bool isTextBody)
            => isTextBody && resp.MimeType!.StartsWith("text/gemini");

        public override ParsedResponse? Parse(GeminiResponse resp)
        {
            string[] lines = LineParser.GetLines(resp.BodyText);
            List<string> noPreformatted = LineParser.RemovePreformattedLines(lines);
            var indexableText = GetIndexableContent(noPreformatted);

            List<FoundLink> links = LinkFinder.GetLinks(resp.RequestUrl, noPreformatted).ToList();

            return new GemTextResponse(resp)
            {
                DetectedLanguage = languageDetector.DetectLanguage(indexableText),
                IndexableText = indexableText,
                IsFeed = IsGemFeed(links),
                LineCount = lines.Length,
                Links = links,
                Title = TitleFinder.FindTitle(lines),

                HashTags = HashtagsFinder.GetHashtags(noPreformatted),
                Mentions = MentionsFinder.GetMentions(noPreformatted)
            };
        }

        private string GetIndexableContent(IEnumerable<string> noPreformatted)
        {
            var sb = new StringBuilder();
            foreach (string line in noPreformatted)
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

        /// <summary>
        /// Checks found links for the gemfeed format. If more than 2 are found, consider this a feed
        /// </summary>
        /// <param name="links"></param>
        /// <returns></returns>
        private bool IsGemFeed(List<FoundLink> links)
            => links.Where(x => x.LinkText.Length >= 10 && Iso8601Date.IsMatch(x.LinkText)).Count() >= 2;
        
    }
}

