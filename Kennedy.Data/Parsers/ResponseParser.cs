


using Gemini.Net;
using Kennedy.Data.Models;
using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
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

        public AbstractResponse Parse(GeminiResponse resp)
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
            return new AbstractResponse
            {
                ContentType = ContentType.Binary
            };
        }
    }
}
