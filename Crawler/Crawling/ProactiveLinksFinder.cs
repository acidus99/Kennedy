using Gemini.Net;
using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Crawling;

/// <summary>
/// Looks at a response and determines any proactive links we should also send
/// </summary>
public class ProactiveLinksFinder : ILinksFinder
{
    object locker;
    Bag<string> SeenAuthorities;

    public ProactiveLinksFinder()
    {
        locker = new object();
        SeenAuthorities = new Bag<string>();
    }

    public IEnumerable<FoundLink>? FindLinks(GeminiResponse response)
    {
        //if we couldn't reach the domain, there are no proactive requests for it
        if (!response.IsAvailable)
        {
            return null;
        }

        string key = response.RequestUrl.Authority;
        bool addLinks = false;

        lock (locker)
        {
            if (!SeenAuthorities.Contains(key))
            {
                SeenAuthorities.Add(key);
                addLinks = true;
            }
        }

        if (addLinks)
        {
            return GetProactiveLinksForDomain(response.RequestUrl);
        }
        return null;
    }

    private IEnumerable<FoundLink> GetProactiveLinksForDomain(GeminiUrl request)
    {
        return new List<FoundLink>
        {
            CreateLink(request.Authority, "/favicon.txt"),
            CreateLink(request.Authority, "/.well-known/security.txt"),
        };
    }

    private FoundLink CreateLink(string authority, string path)
       => new FoundLink
       {
           Url = new GeminiUrl($"gemini://{authority}{path}"),
           IsExternal = false,
           LinkText = ""
       };
}
