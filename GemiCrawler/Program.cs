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
            //var url = "gemini://mozz.us/";
            //var url = "gemini://marginalia.nu:1965/log";
            var url = "gemini://geminispace.info/known-hosts";



            var crawler = new Crawler();

            crawler.AddSeed(url);
            crawler.DoCrawl();


            return;


            ////Scanner.DoIt();

            //var requestor = new GemiRequestor();

            //var gurl = new GemiUrl(url);

            //var resp = requestor.Request(gurl);

            //Console.WriteLine(resp.ToString());

            //var links = LinkFinder.ExtractUrls(gurl, resp);
            //Console.WriteLine($"{links.Count} found!");
            //foreach(var link in links)
            //{
            //    Console.WriteLine($"\t{link}");
            //}
        }

    }

    

}
