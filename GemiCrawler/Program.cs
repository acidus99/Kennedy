using System;
using Gemi.Net;
using GemiCrawler.DataStore;

namespace GemiCrawler
{
    class Program
    {
        static void Main(string[] args)
        {




            var db = new CrawlDbContext();

            var resp = new StoredResponse
            {
                Url = "gemini://mozz.us/",
                MimeType = "text/gemini",
                BodyText = "This article covers configuration that can be applied to a model targeting any data store and that which can be applied when targeting any relational database. Providers may also enable configuration that is specific to a particular data store. For documentation on provider specific configuration see more."
            };
            db.Add(resp);
            db.SaveChanges();


            //var url = "gemini://billy.flounder.online/tech-links.gmi";
            //var url = "gemini://mozz.us/jetforce/logo.jpg";
            //var url = "gemini://gemini.circumlunar.space/docs/faq.gmi";
            //var url = "gemini://capsule.ghislainmary.fr/photo/";
            //var url = "gemini://billy.flounder.online/index.gmi";
            var url = "gemini://mozz.us/";
            //var url = "gemini://marginalia.nu:1965/log";


            var crawler = new Crawler(80,300000);

            crawler.AddSeed(url);
            crawler.AddSeed("gemini://geminispace.info/known-hosts");
        
            crawler.DoCrawl();

            return;
        }

    }

    

}
