using System;
using System.Linq;
using System.Collections.Generic;
using Gemini.Net;

using Kennedy.Crawler.Modules;
using Kennedy.Crawler.Support;



namespace Kennedy.Crawler
{
    class Program
    {
        static void Main(string[] args)
        {


            GeminiRequestor requestor = new GeminiRequestor();
            var url = "gemini://ttrpgs.org:1965/";
            //var url = "gemini://cyberpunksin.space:1965/";

            var result = requestor.Request(url);

            return;


            //DomainScanner.DoIt();
            //DomainScanner.ProcessDomain("gmi.bacardi55.io");
            //IndexLoader.BuildIndexes();
            //var title = TitleChecker.GetTitle("gemini://geminispace.info:1965/");
            //return;

            //PopularityCalculator calc = new PopularityCalculator();
            //calc.Rank();
            //return;

            var domainsFile = $"{Crawler.DataDirectory}capsules-to-scan.txt";

            //RobotsFetcher.DoIt(domainsFile);
            var crawler = new Crawler(80,400000);

            crawler.PreheatDns(domainsFile);
            crawler.AddSeedFile(domainsFile);
            crawler.DoCrawl();

            return;
        }
    }
}
