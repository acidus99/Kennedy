using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Data.Parsers
{
    public class ResponseParser
    {
        BinaryParser binaryParser;
        TextParser textParser;

        public ResponseParser()
        {
            binaryParser = new BinaryParser();
            textParser = new TextParser();
        }

        public ParsedResponse Parse(GeminiUrl url, byte[] completeResponse)
        {
            GeminiResponse resp = GeminiParser.ParseResponseBytes(url, completeResponse);
            return Parse(resp);
        }

        public ParsedResponse Parse(GeminiResponse resp)
        {
            ParsedResponse? parsedResponse = TryParseRedirect(resp);
            if(parsedResponse != null)
            {
                return parsedResponse;
            }

            if (!resp.IsSuccess || !resp.HasBody)
            {
                //unknown response
                return new ParsedResponse(resp);
            }

            //check for known binary formats
            parsedResponse = binaryParser.Parse(resp);

            if(parsedResponse != null)
            {
                return parsedResponse;
            }

            //check for text formats
            parsedResponse = textParser.Parse(resp);

            if(parsedResponse != null)
            {
                return parsedResponse;
            }

            //fail back on binary
            return new ParsedResponse(resp)
            {
                FormatType = ContentType.Binary
            };
        }

        private ParsedResponse? TryParseRedirect(GeminiResponse resp)
        {
            if (resp.IsRedirect)
            {
                var link = FoundLink.Create(resp.RequestUrl, resp.Meta);
                if (link != null)
                {
                    return new ParsedResponse(resp)
                    {
                        Links = { link }
                    };
                }
            }
            return null;
        }
    }
}
