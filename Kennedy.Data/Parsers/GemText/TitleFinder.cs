using System.Text.RegularExpressions;

namespace Kennedy.Data.Parsers.GemText;

/// <summary>
/// Attempts to determine a "title" for a gemtext page
/// Rules:
/// - Look for any header
/// - Look the first preformatted text section for an alt text (used for
///     ascii art logos)
/// </summary>
public static class TitleFinder
{
    static readonly Regex headingRegex = new Regex(@"^(#+)\s*(.+)", RegexOptions.Compiled);

    public static string? FindTitle(IEnumerable<string> bodyLines)
    { 
        string? title = TryHeaders(bodyLines);
        if(title != null)
        {
            return title;
        }
        return TryPreformatted(bodyLines);
    }

    /// <summary>
    /// extracts a title from the first non-empty H1, if present
    /// </summary>
    /// <param name="bodyLines"></param>
    /// <returns></returns>
    private static string? TryHeaders(IEnumerable<string> bodyLines)
    {
        return (from line in bodyLines
                 let match = headingRegex.Match(line)
                 where match.Success
                 let headerText = match.Groups[2].Value.Trim()
                 where headerText.Length > 0
                 select headerText).FirstOrDefault();
    }

    /// <summary>
    /// extracts a title from any caption on the first preformatted block
    /// </summary>
    /// <param name="bodyLines"></param>
    /// <returns></returns>
    private static string? TryPreformatted(IEnumerable<string> bodyLines)
    {
        var preLine = bodyLines
               .Where(x => x.StartsWith("```"))
               .FirstOrDefault();

        return (preLine?.Length > 3) ?
            preLine.Substring(3).Trim() :
            null;
    }
}