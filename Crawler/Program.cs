using System;
using Gemini.Net;

using Kennedy.Crawler.Crawling;

namespace Kennedy.Crawler
{
    class Program
    {
        static void Main(string[] args)
        {
            HandleArgs(args);

            var crawler = new WebCrawler(40, 600000);

            crawler.AddSeed("gemini://mozz.us/");
            crawler.AddSeed("gemini://kennedy.gemi.dev/observatory/known-hosts");
            crawler.AddSeed("gemini://spam.works/");
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
