using System;
using Gemini.Net;

namespace Kennedy.Blazer
{
    class Program
    {
        static void Main(string[] args)
        {

            //var url = "gemini://billy.flounder.online/tech-links.gmi";
            //var url = "gemini://makeworld.gq/cgi-bin/gemini-irc";
            //var url = "gemini://gemini.circumlunar.space/docs/faq.gmi";
            //var url = "gemini://capsule.ghislainmary.fr/photo/";
            //var url = "gemini://billy.flounder.online/index.gmi";
            var url = "gemini://mozz.us/";
            //var url = "gemini://marginalia.nu:1965/log";
            //var url = "gemini://geminispace.info/known-hosts";

            var crawler = new Crawler();

            crawler.AddSeed(url);
            crawler.DoCrawl();

            return;
        }

    }
}
