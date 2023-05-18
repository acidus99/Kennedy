using System;
using System.Text;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
{
	public class GemTextResponseParser : AbstractResponseParser
	{
        LanguageDetector languageDetector = new LanguageDetector();

        public override bool CanParse(GeminiResponse resp)
            // If we are successful, there mus be a MIME type, since the specification defines one if missing.
            => resp.HasBody && resp.IsSuccess && resp.MimeType!.StartsWith("text/gemini");

        public override ParsedResponse Parse(GeminiResponse resp)
        {
            string[] lines = LineParser.GetLines(resp.BodyText);
            IEnumerable<string> noPreformatted = LineParser.RemovePreformattedLines(lines);
            var indexableText = GetIndexableContent(noPreformatted);

            return new GemTextResponse(resp)
            {
                FilteredBody = indexableText,
                Links = LinkFinder.GetLinks(resp.RequestUrl, noPreformatted).ToList(),
                Title = TitleFinder.FindTitle(lines),
                LineCount = lines.Length,
                DetectedLanguage = languageDetector.DetectLanguage(indexableText),
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

