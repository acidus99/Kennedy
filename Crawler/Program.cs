using System;
using Gemini.Net;

using Kennedy.Crawler.Crawling;

using Kennedy.Crawler.TopicIndexes;


namespace Kennedy.Crawler
{
    class Program
    {
        static void Main(string[] args)
        {
            HandleArgs(args);

            //Support.GraphGenerator graph = new Support.GraphGenerator(CrawlerOptions.DataStore);

            //Support.SiteHealthReport report = new Support.SiteHealthReport(CrawlerOptions.DataStore);
            //report.WriteReport("/tmp/report.gmi", "gemi.dev");
            //return;

            //var url = "gemini://billy.flounder.online/tech-links.gmi";
            //var url = "gemini://makeworld.gq/cgi-bin/gemini-irc";
            //var url = "gemini://gemini.circumlunar.space/docs/faq.gmi";
            //var url = "gemini://capsule.ghislainmary.fr/photo/";
            //var url = "gemini://billy.flounder.online/index.gmi";
            var url = "gemini://mozz.us/";
            //var url = "gemini://marginalia.nu:1965/log";
            //var url = "gemini://geminispace.info/known-hosts";

            var crawler = new WebCrawler(40, 500000);

            crawler.AddSeed(url);
            crawler.DoCrawl();

            TopicGenerator.BuildFiles(CrawlerOptions.PublicRoot);
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
