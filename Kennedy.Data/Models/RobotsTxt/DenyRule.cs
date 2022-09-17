using System;

namespace Kennedy.Data.Models.RobotsTxt
{
    public class DenyRule
    {
        public string Path { get; private set; }

        public bool IsAllowAll
            => String.IsNullOrEmpty(Path);

        public bool HasWildcard
            => Path.Contains("*");

        public DenyRule(string value)
        {
            Path = value ?? "";

            if (Path.Length > 0 && !Path.StartsWith("/"))
            {
                Path = "/" + Path;
            }

            //try and fix trailing wildcards
            if(Path.EndsWith("*"))
            {
                Path = Path.Substring(0, Path.Length-1);
            }
        }
    }
}