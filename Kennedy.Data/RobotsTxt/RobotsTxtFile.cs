using System.Data;

namespace Kennedy.Data.RobotsTxt;

/// <summary>
/// Parsed result of a robots.txt file, organized as a dictionary of user-agent → deny rules.
/// The special user-agent key <c>"*"</c> holds wildcard rules that apply to all crawlers.
/// Use <see cref="IsPathAllowed"/> to check whether a crawler may fetch a given path.
/// </summary>
public class RobotsTxtFile
{
    /// <summary>
    /// Deny rules grouped by lower-cased user-agent string.
    /// Key <c>"*"</c> represents the wildcard user-agent (applies to everyone).
    /// </summary>
    public readonly Dictionary<string, List<DenyRule>> Rules;

    /// <summary>True when at least one <see cref="DenyRule"/> with a non-empty path was parsed.</summary>
    public bool HasValidRules => (Rules.Values.Sum(x=>x.Count) > 0);

    internal RobotsTxtFile()
    {
        Rules = new Dictionary<string, List<DenyRule>>();
    }

    /// <summary>Registers <paramref name="denyRule"/> for each user-agent in <paramref name="userAgents"/>.</summary>
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

    /// <summary>
    /// Returns true if <paramref name="userAgent"/> is permitted to fetch <paramref name="path"/>.
    /// Wildcard (<c>"*"</c>) rules are checked first; if any deny rule's path is a prefix of
    /// <paramref name="path"/>, access is denied. Then specific user-agent rules are checked the same way.
    /// </summary>
    public bool IsPathAllowed(string userAgent, string path)
    {

        if (Rules.ContainsKey("*"))
        {
            //do global rules
            foreach (var rule in Rules["*"])
            {
                if (rule.IsAllowAll)
                {
                    //ignore it
                } else if (path.StartsWith(rule.Path))
                {
                    return false;
                }
            }
        }

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
