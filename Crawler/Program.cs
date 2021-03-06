using System;

using Kennedy.Crawler.Support;

namespace Kennedy.Crawler
{
    class Program
    {
        static void Main(string[] args)
        {
            HandleArgs(args);

            Console.WriteLine("Kennedy Crawler!");
            var domainsFile = $"{CrawlerOptions.DataDirectory}capsules-to-scan.txt";

            Console.WriteLine("Stage 1: Fetching Robots");
            RobotsFetcher.DoIt(domainsFile);

            Console.WriteLine("Stage 2: Preheating DNS");
            var crawler = new Crawler(80, 500000);
            crawler.PreheatDns(domainsFile);

            Console.WriteLine("Stage 3: Crawling");
            crawler.AddSeedFile(domainsFile);
            crawler.DoCrawl();

            Console.WriteLine("Stage 4: Calculating Poprank");
            PopularityCalculator calc = new PopularityCalculator();
            calc.Rank();

            Console.WriteLine("Stage 5: Building Indexes: Topics and Mentions ");
            IndexLoader.BuildIndexes();
            return;
        }

        static void HandleArgs(string[] args)
        {
            try
            {
                if (System.IO.Directory.Exists(args[0]))
                {
                    CrawlerOptions.DataDirectory = args[0];
                }
            }
            catch (Exception)
            {

            }
        }
    }
}