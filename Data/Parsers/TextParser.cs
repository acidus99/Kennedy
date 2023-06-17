using System;
using Gemini.Net;

namespace Kennedy.Data.Parsers
{
	public class TextParser
	{
        MimeSniffer sniffer = new MimeSniffer();
        List<AbstractTextParser> parsers = new List<AbstractTextParser>
        {
            new GemTextResponseParser(),
            new PlainTextResponseParser(),
        };

        public ParsedResponse? Parse(GeminiResponse resp)
        {
            if (resp.BodyBytes == null)
            {
                throw new ArgumentNullException(nameof(resp), "Response BodyBytes cannot be null");
            }

            bool isTextBody = sniffer.IsText(resp.BodyBytes);

            foreach (var parser in parsers)
            {
                if (parser.CanParse(resp, isTextBody))
                {
                    var doc = parser.Parse(resp);
                    if (doc != null)
                    {
                        return doc;
                    }
                }
            }

            return null;
        }

    }
}

