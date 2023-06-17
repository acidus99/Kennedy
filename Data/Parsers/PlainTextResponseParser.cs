using System;
using System.Text;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
{
    /// <summary>
    /// Handles both properly labeled "text/plain" responses,
    /// and responses that ARE text, but don't have a "text/" mimetype.
    /// This means that text that isn't plain text, like "text/xml" or "text/x-diff" will not be parsed.
    /// This is on purpose so I don't pollute the text indexes
    /// </summary>
	public class PlainTextResponseParser : AbstractTextParser
	{
        LanguageDetector languageDetector = new LanguageDetector();

        public override bool CanParse(GeminiResponse resp, bool isTextBody)
        {
            if (!isTextBody)
            {
                return false;
            }

            return resp.MimeType == "text/plain";
            //return resp.MimeType!.StartsWith("text/plain") || !resp.MimeType!.StartsWith("text/");
        }

        public override ParsedResponse? Parse(GeminiResponse resp)
        {
            return new PlainTextResponse(resp)
            {
                DetectedLanguage = languageDetector.DetectLanguage(resp.BodyText),
            };
        }
    }
}

