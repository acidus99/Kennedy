using System;
using System.Linq;
using System.Collections.Generic;
using Gemi.Net;

using GemiCrawler.Support;



namespace GemiCrawler
{
    class Program
    {
        static void Main(string[] args)
        {

            FTSLoader loader = new FTSLoader(Crawler.DataDirectory);
            loader.LoadDocuments();

            return;
            var crawler = new Crawler(80,400000);

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
