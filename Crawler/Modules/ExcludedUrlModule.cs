using System;
using System.Collections.Generic;
using Gemini.Net;
using Kennedy.Crawler.Utils;
using System.IO;

namespace Kennedy.Crawler.Modules
{
    public class ExcludedUrlModule : AbstractUrlModule
    {
        /// <summary>
        /// table to URLs prefixes to exclide, sorted by URL authority
        /// </summary>
        Dictionary<string, List<string>> excludedUrls;

        ThreadSafeCounter excludedCounter = new ThreadSafeCounter();

        public ExcludedUrlModule(string dataFile)
            : base("ExcludedUrl")
        {
            excludedUrls = new Dictionary<string, List<string>>();
            LoadExclusions(dataFile);
        }

        private void LoadExclusions(string dataFile)
        {
            foreach(string l in File.ReadAllLines(dataFile))
            {
                var line = l.Trim();
                if(line.Length < 1 || line[0] == '#')
                {
                    continue;
                }
                GeminiUrl url = new GeminiUrl(line);
                if(!excludedUrls.ContainsKey(url.Authority))
                {
                    excludedUrls[url.Authority] = new List<string>();
                }
                excludedUrls[url.Authority].Add(url.NormalizedUrl);
            }
        }

        /// <summary>
        /// Checks a URL against a list of deny patterns
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Is this URL not blocked by a deny pattern<returns>
        public override bool IsUrlAllowed(GeminiUrl url)
        {
            processedCounter.Increment();
            if(!excludedUrls.ContainsKey(url.Authority))
            {
                return true;
            }
            var normalized = url.NormalizedUrl;
            foreach(string urlPrefix in excludedUrls[url.Authority])
            {
                if(normalized.StartsWith(urlPrefix))
                {
                    excludedCounter.Increment();
                    return false;
                }
            }
            return true;
        }

        protected override string GetStatusMesssage()
            => $"Urls seen: {processedCounter.Count} Excluded: {excludedCounter.Count}";
    }
}
