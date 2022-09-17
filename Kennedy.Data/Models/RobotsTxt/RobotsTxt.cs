using System;
using System.Collections.Generic;
using System.Linq;

namespace Kennedy.Data.Models.RobotsTxt
{
    public class RobotsTxt
    {
        public bool IsMalformed { get; set; } = false;

        /// <summary>
        /// Rules that apply to all user-agents
        /// </summary>
        public List<DenyRule> GlobalRules = new List<DenyRule>();

        /// <summary>
        /// Rules that apply to a specific user-agent
        /// </summary>
        public Dictionary<string, List<DenyRule>> SpecificRules = new Dictionary<string, List<DenyRule>>();

        public int RuleCount => GlobalRules.Count + SpecificRules.Keys.Count;

        public bool HasRules => (RuleCount > 0);

        public bool HasUnknown { get; set; } = false;

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
