using System;
using Gemini.Net;
using Kennedy.Crawler.Utils;
using Kennedy.Data;

namespace Kennedy.Crawler.Filters
{
	public class DenyListFilter : IUrlFilter
	{
        static readonly UrlFilterResult Denied = new UrlFilterResult(false, "Matches Deny List Filter");

        /// <summary>
        /// table to URLs prefixes to exclide, sorted by URL authority
        /// </summary>
        Dictionary<string, List<string>> excludedUrls;

        List<string> globalRules;

        ThreadSafeCounter excludedCounter = new ThreadSafeCounter();

        public DenyListFilter()
            :this(CrawlerOptions.ConfigDir)
        {
        }

        public DenyListFilter(string configDir)
        {
            var dataFile = configDir + "block-list.txt";
            excludedUrls = new Dictionary<string, List<string>>();
            globalRules = new List<string>();
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

                if (line.EndsWith("*"))
                {
                    line = line.Replace("*", "");
                    //global rule
                    globalRules.Add(line);
                }
                else
                {
                    var url = new GeminiUrl(line);
                    if (!excludedUrls.ContainsKey(url.Authority))
                    {
                        excludedUrls[url.Authority] = new List<string>();
                    }
                    excludedUrls[url.Authority].Add(url.NormalizedUrl);
                }
            }
        }

        /// <summary>
        /// Checks a URL against a list of deny patterns
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        ///
        public UrlFilterResult IsUrlAllowed(UrlFrontierEntry entry)
            => IsUrlAllowed(entry.Url);

        public UrlFilterResult IsUrlAllowed(GeminiUrl url)
        {
            var normalized = url.NormalizedUrl;

            if (globalRules.Where(x => normalized.StartsWith(x)).Count() > 0)
            {
                return Denied;
            }

            if (!excludedUrls.ContainsKey(url.Authority))
            {
                return UrlFilterResult.Allowed;
            }

            foreach (string urlPrefix in excludedUrls[url.Authority])
            {
                if (normalized.StartsWith(urlPrefix))
                {
                    excludedCounter.Increment();
                    return Denied;
                }
            }
            return UrlFilterResult.Allowed;
        }
    }
}

