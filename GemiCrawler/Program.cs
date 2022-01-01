using System;
using Gemi.Net;

namespace GemiCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            var crawler = new Crawler(80,300000);

            bool doNew = true;
            if (doNew)
            {
                crawler.AddSeed("gemini://gemini.bortzmeyer.org/software/lupa/lupa-capsules.gmi");
                crawler.AddSeed("gemini://tlgs.one/known-hosts");
                crawler.AddSeed("gemini://geminispace.info/known-hosts");
            }
            else
            {
                crawler.LoadPreviousResults();
            }
            crawler.DoCrawl();

            return;
        }
    }
}
