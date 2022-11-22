using System;
using System.Collections.Generic;
using System.Linq;

namespace Kennedy.Blazer.RobotsTxt
{
    public class Robots
    {

        public bool IsMalformed { get; private set; }

        /// <summary>
        /// Rules that apply to all user-agents
        /// </summary>
        List<DenyRule> GlobalRules;

        /// <summary>
        /// Rules that apply to a specific user-agent
        /// </summary>
        Dictionary<string, List<DenyRule>> SpecificRules;

        int ruleCount;

        public bool HasRules => (ruleCount > 0);

        public bool HasUnknown { get; private set; }

        public Robots(string contents)
        {
            if (String.IsNullOrWhiteSpace(contents))
            {
                return;
            }

            string[] lines = contents
                .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !String.IsNullOrWhiteSpace(l))
                .ToArray();
            if (lines.Length == 0)
            {
                return;
            }
            parseLines(lines);
        }

        private void parseLines(string[] lines)
        {
            IsMalformed = false;
            HasUnknown = false;
            ruleCount = 0;
            GlobalRules = new List<DenyRule>();
            SpecificRules = new Dictionary<string, List<DenyRule>>();

            bool inUserAgent = false;
            var currentUserAgents = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var robotsLine = new Line(line);

                switch (robotsLine.Type)
                {
                    case LineType.Comment: //ignore the comments
                        continue;

                    case LineType.UserAgent:
                        if (!inUserAgent)
                        {
                            currentUserAgents.Clear();
                        }
                        inUserAgent = true;
                        currentUserAgents.Add(robotsLine.Value);
                        continue;

                    case LineType.DenyRule:
                        if (currentUserAgents.Count == 0)
                        {
                            //can't have deny rules without user-agents first
                            IsMalformed = true;
                            return;
                        }
                        inUserAgent = false;
                        ruleCount++;
                        AddDenyRule(currentUserAgents, new DenyRule(robotsLine));
                        continue;

                    case LineType.Unknown:
                        HasUnknown = true;
                        continue;
                }

            }
        }

        private void AddDenyRule(List<string> userAgents, DenyRule denyRule)
        {
            //Gemini does not allow wildcards in robots
            //ignore wildcard rules
            if(denyRule.HasWildcard)
            {
                IsMalformed = true;
                return;
            }

            foreach(var userAgent in userAgents)
            {
                if (userAgent == "*")
                {
                    GlobalRules.Add(denyRule);
                }
                else
                {
                    if(!SpecificRules.ContainsKey(userAgent))
                    {
                        SpecificRules[userAgent] = new List<DenyRule>();
                    }
                    SpecificRules[userAgent].Add(denyRule);
                }
            }
        }

        public bool IsPathAllowed(string userAgent, string path)
        {
            //assume allowed
            bool ret = true;

            if(!HasRules)
            {
                return ret;
            }

            //check against global rules
            foreach(var rule in GlobalRules)
            {
                if(rule.IsAllowAll)
                {
                    ret = true;
                } else if(path.StartsWith(rule.Path))
                {
                    ret = false;
                }
            }
            if(SpecificRules.ContainsKey(userAgent))
            {
                foreach (var rule in SpecificRules[userAgent])
                {
                    if (rule.IsAllowAll)
                    {
                        ret = true;
                    }
                    else if (path.StartsWith(rule.Path))
                    {
                        ret = false;
                    }
                }
            }
            return ret;
        }

    }
}
