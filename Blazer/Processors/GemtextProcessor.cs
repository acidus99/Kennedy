using System;
using System.Text.RegularExpressions;
using Gemini.Net;
using Kennedy.Blazer.Frontiers;

namespace Kennedy.Blazer.Processors
{
    /// <summary>
    /// extracts links from Gemtext and adds them to the URL frontier
    /// </summary>
	public class GemtextProcessor : IResponseProcessor
	{
        IUrlFrontier UrlFrontier;

        static readonly Regex linkLine = new Regex(@"^=>\s*([^\s]+)\s*(.*)", RegexOptions.Compiled);

        public GemtextProcessor(IUrlFrontier urlFrontier)
        {
            UrlFrontier = urlFrontier;
        }

        public bool CanProcessResponse(GeminiResponse response)
            => response.IsSuccess &&
                response.HasBody &&
                response.MimeType.StartsWith("text/gemini");

        public void ProcessResponse(GeminiResponse response)
        {
            var foundLinks =
                       (from line in response.BodyText.Split("\n")
                        let match = linkLine.Match(line)
                        where match.Success
                        let link = GeminiUrl.MakeUrl(response.RequestUrl, match.Groups[1].Value)
                        where link != null
                        select link);

            UrlFrontier.AddUrls(foundLinks);
        }
    }
}