using System;

using Kennedy.Data.Models.RobotsTxt;
namespace Kennedy.Data.Parsers
{
	public class RobotsTxtParser
	{
        RobotsTxt ret;

		public RobotsTxtParser()
		{

        }

        public RobotsTxt Parse(string contents)
        {
            ret = new RobotsTxt();

            if (string.IsNullOrWhiteSpace(contents))
            {
                return ret;
            }

            string[] lines = contents
                .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !String.IsNullOrWhiteSpace(l))
                .ToArray();
            if (lines.Length == 0)
            {
                return ret;
            }
            parseLines(lines);
            return ret;
        }

        private void parseLines(string[] lines)
        {
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
                            ret.IsMalformed = true;
                            return;
                        }
                        inUserAgent = false;
                        AddDenyRule(currentUserAgents, new DenyRule(robotsLine.Value));
                        continue;

                    case LineType.Unknown:
                        ret.HasUnknown = true;
                        continue;
                }

            }
        }

        private void AddDenyRule(List<string> userAgents, DenyRule denyRule)
        {
            //Gemini does not allow wildcards in robots
            //ignore wildcard rules
            if (denyRule.HasWildcard)
            {
                ret.IsMalformed = true;
                return;
            }

            foreach (var userAgent in userAgents)
            {
                if (userAgent == "*")
                {
                    ret.GlobalRules.Add(denyRule);
                }
                else
                {
                    if (!ret.SpecificRules.ContainsKey(userAgent))
                    {
                        ret.SpecificRules[userAgent] = new List<DenyRule>();
                    }
                    ret.SpecificRules[userAgent].Add(denyRule);
                }
            }
        }

        private class Line
        {
            public LineType Type { get; private set; }
            public string Raw { get; private set; }
            public string Field { get; private set; }
            public string Value { get; private set; }

            public Line(string line)
            {
                if (String.IsNullOrWhiteSpace(line))
                {
                    throw new ArgumentException("Can't create a new instance of Line class with an empty line.", "line");
                }

                Raw = line;
                line = line.Trim();

                if (line.StartsWith("#"))
                {
                    // whole line is comment
                    Type = LineType.Comment;
                    return;
                }

                // if line contains comments, get rid of them
                if (line.IndexOf('#') > 0)
                {
                    line = line.Remove(line.IndexOf('#'));
                }

                string field = GetField(line);
                if (String.IsNullOrWhiteSpace(field))
                {
                    // If could not find the first ':' char or if there wasn't a field declaration before ':'
                    Type = LineType.Unknown;
                    return;
                }

                Field = field.Trim();
                Type = GetLineType(field.Trim().ToLowerInvariant());
                Value = line.Substring(field.Length + 1).Trim(); //we remove <field>:
            }

            LineType GetLineType(string field)
            {
                switch (field)
                {
                    case "user-agent":
                        return LineType.UserAgent;
                    case "disallow":
                        return LineType.DenyRule;
                    default:
                        return LineType.Unknown;
                }
            }

            string GetField(string line)
            {
                var index = line.IndexOf(':');
                if (index == -1)
                {
                    return String.Empty;
                }

                return line.Substring(0, index);
            }
        }

        private enum LineType
        {
            Comment,
            UserAgent,
            DenyRule,
            Unknown
        }

    }
}