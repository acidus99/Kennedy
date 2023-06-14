using System;
using System.Text;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
{
	public class PlainTextResponseParser : AbstractResponseParser
	{
        LanguageDetector languageDetector = new LanguageDetector();

        public override bool CanParse(GeminiResponse resp)
            // If we are successful, there must be a MIME type, since the specification defines one if missing.
            => resp.IsSuccess && resp.HasBodyText && resp.MimeType!.StartsWith("text/plain");

        public override ParsedResponse Parse(GeminiResponse resp)
        {
            return new PlainTextResponse(resp)
            {
                DetectedLanguage = languageDetector.DetectLanguage(resp.BodyText),
            };
        }
    }
}

