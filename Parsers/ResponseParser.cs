﻿using Gemini.Net;
using Kennedy.Data;


namespace Kennedy.Parsers
{
    public class ResponseParser
    {
        List<AbstractResponseParser> parsers;

        public ResponseParser()
        {
            parsers = new List<AbstractResponseParser>()
            {
                new RedirectResponseParser(),
                new GemTextResponseParser(),
                new ImageResponseParser()
            };
        }

        public ParsedResponse Parse(GeminiResponse resp)
        {
            foreach (var parser in parsers)
            {
                if (parser.CanParse(resp))
                {
                    var doc = parser.Parse(resp);
                    if (doc != null)
                    {
                        return doc;
                    }
                }
            }
            return new ParsedResponse(resp)
            {
                ContentType = ContentType.Binary
            };
        }
    }
}