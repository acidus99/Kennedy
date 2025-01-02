namespace Kennedy.Data.RobotsTxt;

public class DenyRule
{
    public string Path { get; private set; }

    public bool IsAllowAll
        => String.IsNullOrEmpty(Path);

    public DenyRule(string line)
    {
        Path = line;

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
