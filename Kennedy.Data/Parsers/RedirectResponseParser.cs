using System;
using Gemini.Net;
using Kennedy.Data.Models;

namespace Kennedy.Data.Parsers
{
    public class RedirectResponseParser : AbstractResponseParser
    {
        public override bool CanParse(GeminiResponse resp)
            => resp.IsRedirect;

        public override AbstractResponse Parse(GeminiResponse resp)
        {
            var ret = new AbstractResponse();
            var link = FoundLink.Create(resp.RequestUrl, resp.Meta);
            if (link != null)
            {
                ret.Links.Add(link);
            }
            return ret;
        }
    }
}

