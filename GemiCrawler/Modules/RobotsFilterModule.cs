using System;
using Com.Bekijkhet.RobotsTxt;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gemi.Net;

namespace GemiCrawler.Modules
{
    public class RobotsFilterModule : AbstractModule
    {

        Dictionary<string, Robots> rulesCache;

        public RobotsFilterModule(string dataDirectory)
            : base("ROBOTS-FILTER")
        {
            rulesCache = new Dictionary<string, Robots>();
            LoadFromFolder(dataDirectory);
        }
        
        public bool IsUrlAllowed(GemiUrl url)
        {
            if (!rulesCache.ContainsKey(url.Authority))
            {
                return true;
            }
            return rulesCache[url.Authority].IsPathAllowed("indexer", url.Path);
        }

        private void LoadFromFolder(string dataDir)
        {
            DirectoryInfo d = new DirectoryInfo(dataDir);

            var files = d.GetFiles("*.txt");
            if (files.Length < 1)
            {
                Console.WriteLine(CreateLogLine("Warning! No Robots.txt Loaded!"));
                return;
            }

            foreach (var file in files)
            {
                string contents = File.ReadAllText(file.FullName);
                string authority = GetAuthority(contents);
                if(!rulesCache.ContainsKey(authority))
                {
                    rulesCache[authority] = new Robots(contents);
                } else
                {
                    Console.WriteLine(CreateLogLine($"Warning! Duplicate robots.txt detected for '{authority}'"));
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
    }
}
