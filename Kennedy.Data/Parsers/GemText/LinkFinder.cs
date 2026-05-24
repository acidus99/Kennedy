using System.Text.RegularExpressions;
using Gemini.Net;

namespace Kennedy.Data.Parsers.GemText;

/// <summary>
/// Finds hyperlinks in Gemini Text Documents
/// </summary>
public static class LinkFinder
{
    static readonly Regex linkLine = new Regex(@"^=>\s*([^\s]+)\s*(.*)", RegexOptions.Compiled);

    /// <summary>
    /// Returns the link text from a link line. If not a link line, or no link text is present, returns ""
    /// </summary>
    /// <param name="line"></param>
    /// <returns></returns>
    public static string GetLinkText(string line)
        => getLinkText(linkLine.Match(line));

    public static IEnumerable<FoundLink> GetLinks(GeminiUrl requestUrl, IEnumerable<string> bodyLines)
    {
        return (from line in bodyLines
                let match = linkLine.Match(line)
                where ShouldProcess(match)
                let link = Create(requestUrl, match)
                where link != null
                select link);
    }

    private static bool ShouldProcess(Match match)
    {
        if(!match.Success)
        {
            return false;
        }

        var linkUrl = match.Groups[1].Value;

        if (linkUrl.Contains("://") && !linkUrl.StartsWith("gemini://"))
        {
            return false;
        }
        return true;
    }

    private static FoundLink? Create(GeminiUrl pageUrl, Match match)
        => FoundLink.Create(pageUrl, match.Groups[1].Value, getLinkText(match));

    /// <summary>
    /// gives us the text, if any, used with this link
    /// </summary>
    /// <param name="match"></param>
    /// <returns></returns>
    private static string getLinkText(Match match)
        => (match.Groups.Count > 2) ? match.Groups[2].Value : "";

}