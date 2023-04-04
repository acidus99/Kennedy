using System;
using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Parsers
{
    public class RedirectResponseParser : AbstractResponseParser
    {
        public override bool CanParse(GeminiResponse resp)
            => resp.IsRedirect;

        public override ParsedResponse Parse(GeminiResponse resp)
        {
            var link = FoundLink.Create(resp.RequestUrl, resp.Meta);
            if (link != null)
            {
                return new ParsedResponse(resp)
                {
                    Links = { link }
                };
            }
            return null;
        }
    }
}

