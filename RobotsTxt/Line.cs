using System;

namespace Gemini.Net.Crawler.RobotsTxt
{
    internal class Line
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

    internal enum LineType
    {
        Comment,
        UserAgent,
        DenyRule,
        Unknown
    }

}