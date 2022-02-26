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
            //DomainScanner.DoIt();
            //DomainScanner.ProcessDomain("gmi.bacardi55.io");
            IndexLoader.BuildIndexes();
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
