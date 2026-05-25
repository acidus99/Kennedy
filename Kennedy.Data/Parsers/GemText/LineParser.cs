using System.Text.RegularExpressions;

namespace Kennedy.Data.Parsers.GemText;

/// <summary>
/// Low-level Gemtext line utilities: splitting, preformatted-block removal, and heading detection.
/// All methods are stateless and operate on already-split line collections.
/// </summary>
public static class LineParser
{
    static readonly Regex headingRegex = new Regex(@"^(#+)\s*(.+)", RegexOptions.Compiled);

    /// <summary>Splits a Gemtext body on newlines. Returns all lines including preformatted ones.</summary>
    public static string[] GetLines(string bodyText)
        => bodyText.Split('\n');

    /// <summary>
    /// Returns a copy of <paramref name="lines"/> with all preformatted block content removed.
    /// Lines between paired ``` fences are dropped; the fence lines themselves are also dropped.
    /// This is intentionally not LINQ — the toggle state prevents a clean functional expression.
    /// </summary>
    public static List<string> RemovePreformattedLines(IEnumerable<string> lines)
    {
        var ret = new List<string>();
        bool inPre = false;
        foreach(var line in lines)
        {
            if (line.StartsWith("```"))
            {
                inPre = !inPre;
            }
            else if (!inPre)
            {
                ret.Add(line);
            }
        }
        return ret;
    }

    /// <summary>Returns true when <paramref name="line"/> matches the Gemtext heading pattern (<c>#</c>, <c>##</c>, or <c>###</c>).</summary>
    public static bool IsHeading(string line)
        => headingRegex.IsMatch(line);

    /// <summary>
    /// Parses a heading line into (level, text). Level is the count of leading # characters.
    /// Assumes <see cref="IsHeading"/> returned true; results are undefined for non-heading lines.
    /// </summary>
    public static Tuple<int, string> ParseHeading(string line)
    {
        Match match = headingRegex.Match(line);
        return new Tuple<int, string>(getCapture(match, 1).Length, getCapture(match, 2));
    }

    /// <summary>
    /// gives us the text from a group, or "" if any, used with this link
    /// </summary>
    /// <param name="match"></param>
    /// <returns></returns>
    private static string getCapture(Match match, int group)
        => (match.Groups.Count > group) ? match.Groups[group].Value : "";

}