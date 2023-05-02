﻿using System;
using Gemini.Net;

using Kennedy.Crawler.Crawling;

namespace Kennedy.Crawler
{
    class Program
    {
        static void Main(string[] args)
        {
            HandleArgs(args);

            var url = "gemini://mozz.us/";

            var crawler = new WebCrawler(1, 500000);

            //crawler.AddSeed(url);
            //crawler.AddSeed("gemini://kennedy.gemi.dev/observatory/known-hosts");
            crawler.AddSeed("gemini://idiomdrottning.org/inventory.svg");
            //crawler.AddSeed("gemini://gemi.dev/warez-book/");
            crawler.DoCrawl();

            return;
        }

        static void HandleArgs(string[] args)
        {
            if (args.Length == 1)
            {
                CrawlerOptions.OutputBase = args[0];
            }
            
            CrawlerOptions.OutputBase = CrawlerOptions.OutputBase.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) +'/');
            if(!CrawlerOptions.OutputBase.EndsWith(Path.DirectorySeparatorChar))
            {
                CrawlerOptions.OutputBase += Path.DirectorySeparatorChar;
            }
            if(!Directory.Exists(CrawlerOptions.OutputBase))
            {
                Directory.CreateDirectory(CrawlerOptions.OutputBase);
            }
        }
    }
}
