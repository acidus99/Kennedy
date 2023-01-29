using System;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Parsers.GemText;

namespace Kennedy.Parsers
{
	public class GemTextResponseParser : AbstractResponseParser
	{
        LanguageDetector languageDetector = new LanguageDetector();

        public override bool CanParse(GeminiResponse resp)
            => resp.HasBody && resp.IsSuccess && resp.MimeType.StartsWith("text/gemini");

        public override ParsedResponse Parse(GeminiResponse resp)
        {
            var filteredBody = LineParser.FilterBody(resp.BodyText);

            return new GemTextResponse(resp)
            {
                ContentType = ContentType.Text,
                FilteredBody = filteredBody,
                Links = LinkFinder.ExtractBodyLinks(resp.RequestUrl, resp.BodyText).ToList(),
                Title = TitleFinder.ExtractTitle(resp),
                LineCount = CountLines(resp.BodyText),
                Language = languageDetector.DetectLanguage(filteredBody)
            };
        }

        private int CountLines(string bodyText)
            => bodyText.Split("\n").Length;
    }
}

