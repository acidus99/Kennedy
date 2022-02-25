using System;
using System.Linq;
using System.Collections.Generic;
using Gemini.Net;

using Kennedy.Crawler.Support;



namespace Kennedy.Crawler
{
    class Program
    {
        static void Main(string[] args)
        {

            PopularityCalculator calc = new PopularityCalculator();
            calc.Rank();

            //PageRanker ranker = new PageRanker(11);
            //ranker.AddLink("B", "C");
            //ranker.AddLink("C", "B");
            //ranker.AddLink("D", "A");
            //ranker.AddLink("D", "B");
            //ranker.AddLink("E", "B");
            //ranker.AddLink("E", "D");
            //ranker.AddLink("E", "F");
            //ranker.AddLink("F", "E");
            //ranker.AddLink("G", "B");
            //ranker.AddLink("G", "E");
            //ranker.AddLink("H", "B");
            //ranker.AddLink("H", "E");
            //ranker.AddLink("I", "B");
            //ranker.AddLink("I", "E");
            //ranker.AddLink("J", "E");
            //ranker.AddLink("K", "E");
            //ranker.Rank();
            return;

            var crawler = new Crawler(80,300000);

            crawler.AddSeed("gemini://gemini.bortzmeyer.org/software/lupa/lupa-capsules.gmi");
            crawler.AddSeed("gemini://geminispace.info/known-hosts");
            crawler.AddSeed("gemini://tlgs.one/known-hosts");
            
            crawler.DoCrawl();

            return;
        }
    }
}
