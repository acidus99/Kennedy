using System;
using System.Collections.Generic;
using Gemi.Net;
using GemiCrawler.Utils;
using System.IO;

namespace GemiCrawler.Modules
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
            foreach(string line in File.ReadAllLines(dataFile))
            {
                if(line.StartsWith("#"))
                {
                    continue;
                }
                GemiUrl url = new GemiUrl(line);
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
        public override bool IsUrlAllowed(GemiUrl url)
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
