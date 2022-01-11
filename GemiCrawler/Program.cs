using System;
using System.Linq;
using System.Collections.Generic;
using Gemi.Net;

using GemiCrawler.GemText;



namespace GemiCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            var requestor = new GemiRequestor();
            var resp = requestor.Request(new GemiUrl("gemini://going-flying.com:1965/thoughts/"));

            var lines = LineParser.RemovePreformatted(resp.BodyText).ToList();



            var scanner = new GemiCrawler.Support.TermScanner();
            //scanner.FindHashtagsInGemtext(resp.RequestUrl, resp.BodyText);
            scanner.ScanDocs();

            GemiCrawler.Support.TermDumper.Dump(scanner.Hashtags, 3, Crawler.DataDirectory + "hashtags/");

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
