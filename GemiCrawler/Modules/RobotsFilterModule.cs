using System;
//using Com.Bekijkhet.RobotsTxt;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gemi.Net;
using GemiCrawler.RobotsTxt;

using GemiCrawler.Utils;

namespace GemiCrawler.Modules
{
    public class RobotsFilterModule : AbstractModule
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
        
        public bool IsUrlAllowed(GemiUrl url)
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
                Console.WriteLine(CreateLogLine("Warning! No Robots.txt Loaded!"));
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
                        Console.WriteLine(CreateLogLine($"ERROR! Malformed Robots.txt '{file.FullName}'"));
                    } else
                    {
                        rulesCache[authority] = robots;
                    }
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

        public override void OutputStatus(string outputFile)
        {
            File.AppendAllText(outputFile, CreateLogLine($"Urls Rejected: {rejectedCounter.Count}\n"));
        }
    }
}
