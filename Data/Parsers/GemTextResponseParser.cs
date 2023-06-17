using System;
using System.Text;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
{
	public class GemTextResponseParser : AbstractTextParser
	{
        LanguageDetector languageDetector = new LanguageDetector();
        
        public override bool CanParse(GeminiResponse resp, bool isTextBody)
            => isTextBody && resp.MimeType!.StartsWith("text/gemini");

        public override ParsedResponse? Parse(GeminiResponse resp)
        {
            string[] lines = LineParser.GetLines(resp.BodyText);
            List<string> noPreformatted = LineParser.RemovePreformattedLines(lines);
            var indexableText = GetIndexableContent(noPreformatted);

            return new GemTextResponse(resp)
            {
                DetectedLanguage = languageDetector.DetectLanguage(indexableText),
                IndexableText = indexableText,
                LineCount = lines.Length,
                Links = LinkFinder.GetLinks(resp.RequestUrl, noPreformatted).ToList(),
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
    }
}

