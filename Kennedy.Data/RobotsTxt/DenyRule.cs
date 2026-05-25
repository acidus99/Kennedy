namespace Kennedy.Data.RobotsTxt;

/// <summary>
/// Represents a single <c>Disallow:</c> rule from a robots.txt file.
/// Gemini's robots.txt specification supports only prefix-based path matching — no wildcards in the middle.
/// A trailing <c>*</c> is stripped at parse time since all Gemini Disallow rules are implicitly prefix-based.
/// </summary>
public class DenyRule
{
    /// <summary>The path prefix that is blocked. Always starts with <c>/</c>. Empty string means "allow all" (a blank Disallow directive).</summary>
    public string Path { get; private set; }

    /// <summary>The raw original line from the robots.txt file, preserved for diagnostic display.</summary>
    public string Line { get; private set; }

    /// <summary>The 1-based line number in the robots.txt file where this rule appeared.</summary>
    public int LineNumber { get; private set; }

    /// <summary>True when Path is empty, which per the robots.txt spec means "no paths are blocked" (an allow-all rule).</summary>
    public bool IsAllowAll
        => String.IsNullOrEmpty(Path);

    internal DenyRule(string denyPath, string originalLine, int lineNumber)
    {
        Path = denyPath;
        Line = originalLine;
        LineNumber = lineNumber;

        if (Path.Length > 0 && !Path.StartsWith("/"))
        {
            Path = "/" + Path;
        }

        //try and fix trailing wildcards
        if (Path.EndsWith("*"))
        {
            Path = Path.Substring(0, Path.Length - 1);
        }
    }
}
