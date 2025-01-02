namespace Kennedy.Data.RobotsTxt;

public class DenyRule
{
    //The path that is denied
    public string Path { get; private set; }

    /// <summary>
    /// The original line for the robots.txt file
    /// </summary>
    public string Line { get; private set; }

    public int LineNumber { get; private set; }

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
