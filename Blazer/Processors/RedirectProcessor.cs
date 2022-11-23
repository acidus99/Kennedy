using System;
using Gemini.Net;
using Kennedy.Blazer.Frontiers;

namespace Kennedy.Blazer.Processors
{
    /// <summary>
    /// Handle extracting redirect links
    /// </summary>
    public class RedirectProcessor : IResponseProcessor
    {
        IUrlFrontier UrlFrontier;

        public RedirectProcessor(IUrlFrontier urlFrontier)
        {
            UrlFrontier = urlFrontier;
        }

        public bool CanProcessResponse(GeminiResponse response)
            => response.IsRedirect;

        public void ProcessResponse(GeminiResponse response)
        {
            var newUrl = GeminiUrl.MakeUrl(response.RequestUrl, response.Meta);
            if (newUrl != null)
            {
                UrlFrontier.AddUrl(newUrl);
            }
        }
    }
}

