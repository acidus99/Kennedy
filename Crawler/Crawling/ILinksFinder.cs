using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Crawler.Crawling;

public interface ILinksFinder
{
    public IEnumerable<FoundLink>? FindLinks(GeminiResponse response);
}