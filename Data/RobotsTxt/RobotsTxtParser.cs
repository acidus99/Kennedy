namespace Kennedy.Data.RobotsTxt;

public class RobotsTxtParser
{
    List<string> _warnings = new List<string>();

    public IEnumerable<string> Warnings => _warnings;

    private void LogWarning(int lineNumber, string message)
        =>_warnings.Add($"Line {lineNumber}: {message}");

    public RobotsTxtFile Parse(string content)
    {
        RobotsTxtFile ret = new RobotsTxtFile();

        int lineNumber = 0;

        bool inUserAgent = false;
        var currentUserAgents = new List<string>();

        string [] lines = content.Split(Environment.NewLine);
        foreach (var line in lines)
        {
            lineNumber++;
            // Remove inline comments
            var trimmedLine = line.Split('#', 2)[0].Trim();

            // Ignore empty lines after stripping comments
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Split into directive and value
            var parts = trimmedLine.Split(':', 2);
            if (parts.Length != 2)
            {
                LogWarning(lineNumber, "Not a valid directive format! Missing a ':'");
                continue;
            }

            var directive = parts[0].Trim().ToLower();
            var value = parts[1].Trim();

            switch (directive)
            {
                case "user-agent":
                    //are we already in a string of user-agents?
                    if (!inUserAgent)
                    {
                        currentUserAgents.Clear();
                        inUserAgent = true;
                    }
                    currentUserAgents.Add(value.ToLower());
                    break;

                case "disallow":
                    inUserAgent = false;
                    if (currentUserAgents.Count == 0)
                    {
                        //Hit a disallow rule without a user agent!
                        LogWarning(lineNumber, $"Disallow directive without an associated User-Agent directive. This rule will be ignored!");
                        continue;
                    }

                    //if a Disallow rule ends with a wildcard, we can chop it off, since Gemini's robots.txt
                    //only supports prefix matching
                    if (value.EndsWith('*'))
                    {
                        value = value[..^1];
                    }

                    //do we still have a wildcard?
                    if (value.Contains('*'))
                    {
                        //Hit a disallow rule without a user agent!
                        LogWarning(lineNumber, $"Disallow directive containing a wildcard in the middle. This is not supported by Gemini's subset of the robots.txt exclusion standard.This rule will be ignored!");
                        continue;

                    }
                    ret.AddDenyRule(currentUserAgents, new DenyRule(value));
                    //TODO add to robots
                    break;
                case "allow":
                    LogWarning(lineNumber, "Allow rules are not supported by Gemini's subset of the robots.txt exclusion standard. This rule will be ignored!");
                    continue;

                case "crawl-delay":
                    LogWarning(lineNumber, "Crawl-Delay directives are not supported by Gemini's subset of the robots.txt exclusion standard. This will be ignored!");
                    continue;

                case "sitemap":
                    LogWarning(lineNumber, "Sitemap directives are not supported by Gemini's subset of the robots.txt exclusion standard. This will be ignored!");
                    continue;

                default:
                    LogWarning(lineNumber, "Unknown directive. Gemini only supports a small subset of the robots.txt exclusion standard . This will be ignored!");
                    break;
            }
        }

        return ret;
    }

    public static string CreateRobotsUrl(string protocol, string domain, int port)
        => $"{protocol}://{domain}:{port}/robots.txt";
}
