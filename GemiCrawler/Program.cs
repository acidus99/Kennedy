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

        static void UrlTests()
        {
            string[] urls = new string[]
            {
                "gemini://billy.flounder.online/tech-links.gmi?kitty%20cats",
                "gemini://marginalia.nu:1965/log",
                "gemini://mozz.us/jetforce/logo.jpg",
                "gemini://gemini.circumlunar.space/docs/faq.gmi",
                "gemini://capsule.ghislainmary.fr/photo/",
                "gemini://billy.flounder.online/cats/fat/index.gmi",
                "gemini://billy.flounder.online/cats/../../../fat/../index.gmi",
                "gemini://mozz.us",
                "gemini://mozz.us:1977"
            };

            foreach(string url in urls)
            {
                GemiUrl gurl = new GemiUrl(url);
                var path = DocumentStore.GetSavePath(gurl);
                Console.WriteLine($"====\nString:\t'{url}'\nURL:\t'{gurl}'\nPath:\t'{path}'");
            }



        }

    }

    

}
