using System;
using Gemi.Net;

namespace GemiCrawler
{
    class Program
    {
        static void Main(string[] args)
        {

            //var url = "gemini://billy.flounder.online/tech-links.gmi";
            //var url = "gemini://mozz.us/jetforce/logo.jpg";
            //var url = "gemini://gemini.circumlunar.space/docs/faq.gmi";
            //var url = "gemini://capsule.ghislainmary.fr/photo/";
            //var url = "gemini://billy.flounder.online/index.gmi";
            var url = "gemini://mozz.us/";
            //var url = "gemini://marginalia.nu:1965/log";
            //var url = "gemini://geminispace.info/known-hosts";


            RobotsFetcher.DoIt();



            var crawler = new Crawler
            {
                CrawlerThreadCount = 8,
                StopAfterUrlCount = 1000,
            };

            crawler.AddSeed(url);
            crawler.DoCrawl();

            return;
        }

    }

    

}
