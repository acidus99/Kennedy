using System.Data;

namespace Kennedy.Data.RobotsTxt;

public class RobotsTxtFile
{
    /// <summary>
    /// Rules that apply to a specific user-agent
    /// </summary>
    public readonly Dictionary<string, List<DenyRule>> Rules;

    public bool HasValidRules => (Rules.Values.Sum(x=>x.Count) > 0);

    internal RobotsTxtFile()
    {
        Rules = new Dictionary<string, List<DenyRule>>();
    }

    public void AddDenyRule(List<string> userAgents, DenyRule denyRule)
    {
        foreach (var userAgent in userAgents)
        {
                if (!Rules.ContainsKey(userAgent))
                {
                    Rules[userAgent] = new List<DenyRule>();
                }
                Rules[userAgent].Add(denyRule);
        }
    }

    public bool IsPathAllowed(string userAgent, string path)
    {
        if (!Rules.ContainsKey(userAgent))
        {
            //unknown user agent
            return true;
        }

        foreach (var rule in Rules[userAgent])
        {
            if (rule.IsAllowAll)
            {
                //ignore it
            } else if (path.StartsWith(rule.Path))
            {
                return false;
            }
        }

        return true;
    }
}
