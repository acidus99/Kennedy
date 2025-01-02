using System.Data;

namespace Kennedy.Data.RobotsTxt;

public class RobotsTxtFile
{
    /// <summary>
    /// Rules that apply to a specific user-agent
    /// </summary>
    private readonly Dictionary<string, List<DenyRule>> _rules;

    public bool HasValidRules => (_rules.Values.Sum(x=>x.Count) > 0);

    internal RobotsTxtFile()
    {
        _rules = new Dictionary<string, List<DenyRule>>();
    }

    public void AddDenyRule(List<string> userAgents, DenyRule denyRule)
    {
        foreach (var userAgent in userAgents)
        {
                if (!_rules.ContainsKey(userAgent))
                {
                    _rules[userAgent] = new List<DenyRule>();
                }
                _rules[userAgent].Add(denyRule);
        }
    }

    public bool IsPathAllowed(string userAgent, string path)
    {
        if (!_rules.ContainsKey(userAgent))
        {
            //unknown user agent
            return true;
        }

        foreach (var rule in _rules[userAgent])
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
