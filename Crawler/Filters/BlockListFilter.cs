using System;
using Gemini.Net;
using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Filters
{
	public class BlockListFilter : IUrlFilter
	{
        /// <summary>
        /// table to URLs prefixes to exclide, sorted by URL authority
        /// </summary>
        Dictionary<string, List<BlockRule>> siteRules;
        List<BlockRule> globalRules;

        public BlockListFilter()
            :this(CrawlerOptions.ConfigDir)
        {
        }

        public BlockListFilter(string configDir)
        {
            var dataFile = configDir + "block-list.txt";
            siteRules = new Dictionary<string, List<BlockRule>>();
            globalRules = new List<BlockRule>();
            LoadExclusions(dataFile);
        }

        private void LoadExclusions(string dataFile)
        {
            foreach (string l in File.ReadAllLines(dataFile))
            {
                var line = l.Trim();
                if (line.Length < 1 || line[0] == '#')
                {
                    continue;
                }

                var blockRule = new BlockRule(line);
                if(blockRule.IsGlobalRule)
                {
                    globalRules.Add(blockRule);
                } else
                {
                    if (!siteRules.ContainsKey(blockRule.Scope))
                    {
                        siteRules[blockRule.Scope] = new List<BlockRule>();
                    }
                    siteRules[blockRule.Scope].Add(blockRule);
                }
            }
        }

        /// <summary>
        /// Checks a URL against a list of deny patterns
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        ///
        public BlockResult IsUrlAllowed(UrlFrontierEntry entry)
            => IsUrlAllowed(entry.Url);

        public BlockResult IsUrlAllowed(GeminiUrl url)
        {
            foreach(var rule in globalRules)
            {
                if(rule.IsMatch(url))
                {
                    return new BlockResult(false, "Matches Block List", rule.Definition);
                }
            }

            if(!siteRules.ContainsKey(url.Authority))
            {
                //no site specific rules, so its a pass
                return BlockResult.Allowed;
            }

            foreach (var rule in siteRules[url.Authority])
            {
                if(rule.IsMatch(url))
                {
                    return new BlockResult(false, "Matches Block List", rule.Definition);
                }
            }
            return BlockResult.Allowed;
        }
    }
}
