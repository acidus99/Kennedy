using System;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
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
                ContentType = ContentType.Gemtext,
                FilteredBody = filteredBody,
                Links = LinkFinder.ExtractBodyLinks(resp.RequestUrl, resp.BodyText).ToList(),
                Title = TitleFinder.ExtractTitle(resp),
                LineCount = CountLines(resp.BodyText),
                DetectedLanguage = languageDetector.DetectLanguage(filteredBody),
            };
        }

        private int CountLines(string bodyText)
            => bodyText.Split("\n").Length;
    }
}

