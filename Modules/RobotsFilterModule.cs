using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gemini.Net;
using Gemini.Net.Crawler.RobotsTxt;

using Gemini.Net.Crawler.Utils;

namespace Gemini.Net.Crawler.Modules
{
    public class RobotsFilterModule : AbstractUrlModule
    {

        Dictionary<string, Robots> rulesCache;
        ThreadSafeCounter rejectedCounter;


        public RobotsFilterModule(string dataDirectory)
            : base("ROBOTS-FILTER")
        {
            rulesCache = new Dictionary<string, Robots>();
            LoadFromFolder(dataDirectory);

            rejectedCounter = new ThreadSafeCounter();
        }

        public override bool IsUrlAllowed(GeminiUrl url)
        {
            if (!rulesCache.ContainsKey(url.Authority))
            {
                return true;
            }
            bool result = rulesCache[url.Authority].IsPathAllowed("indexer", url.Path);
            if(!result)
            {
                rejectedCounter.Increment();
            }
            return result;
        }

        private void LoadFromFolder(string dataDir)
        {
            DirectoryInfo d = new DirectoryInfo(dataDir);

            var files = d.GetFiles("*.txt");
            if (files.Length < 1)
            {
                Console.WriteLine($"{CreatePrefix()}Warning! No Robots.txt Loaded!");
                return;
            }

            foreach (var file in files)
            {
                string contents = File.ReadAllText(file.FullName);
                string authority = GetAuthority(contents);
                if(!rulesCache.ContainsKey(authority))
                {
                    var robots = new Robots(contents);
                    if(robots.IsMalformed)
                    {
                        Console.WriteLine($"{CreatePrefix()}ERROR! Malformed Robots.txt '{file.FullName}'");
                    } else
                    {
                        rulesCache[authority] = robots;
                    }
                } else
                {
                    Console.WriteLine($"{CreatePrefix()}Warning! Duplicate robots.txt detected for '{authority}'");
                    return;
                }
            }
        }

        /// <summary>
        /// gets the host/port that this robots.txt applies to.
        /// we encode this as a comment in the first line of the robots.txt
        /// when we scrape them
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        private string GetAuthority(string contents)
            =>contents.Split("\n").First().Substring(1);

        protected override string GetStatusMesssage()
            => $"Urls Rejected: {rejectedCounter.Count}";
    }
}
