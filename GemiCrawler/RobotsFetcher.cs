using System;
using System.IO;
using System.Threading.Tasks;
using Gemi.Net;
using GemiCrawler.Utils;

namespace GemiCrawler
{

    /// <summary>
    /// Fetches Robots.txt for Gemini sites from a seed list of capsules
    ///
    /// robots.txt files will be dumped into an output folder with the format "[domain name]-robots.txt"
    /// 
    /// </summary>
    public static class RobotsFetcher
    {
        
        static readonly string domainsFile = $"{Crawler.DataDirectory}capsules-to-scan.txt";

        //folder to output robots.txt into
        static  readonly string outputDir = $"/{Crawler.DataDirectory}/robots/";

        static ThreadSafeCounter requestCounter = new ThreadSafeCounter();
        static ThreadSafeCounter foundCounter = new ThreadSafeCounter();


        public static void DoIt()
        {

            string[] domains = File.ReadAllLines(domainsFile);

            int total = domains.Length;

            int parallelThreadsCount = 16;

            Parallel.ForEach(domains, new ParallelOptions { MaxDegreeOfParallelism = parallelThreadsCount }, domain =>
            {
                int t = requestCounter.Increment();

                GemiRequestor gemiRequestor = new GemiRequestor();

                GemiUrl url = new GemiUrl($"gemini://{domain}/robots.txt");

                var resp = gemiRequestor.Request(url);

                if(gemiRequestor.LastException == null && IsValidRobotsResp(resp))
                {
                    SaveFile(url, resp.BodyText);
                }

                Console.WriteLine($"Progress:\t{t}\t of {total}\tHits:\t{foundCounter.Count}");
            }); //close method invocation 

            int xxx = 5;

        }

        private static bool IsValidRobotsResp(GemiResponse resp)
        {
            if(resp != null && resp.IsSuccess && resp.IsTextResponse)
            {
                if(resp.BodyText.ToLower().Contains("user-agent:"))
                {
                    return true;
                }
            }
            return false;
        }

        private static void SaveFile(GemiUrl url, string text)
        {
            //prepand the host/port in a comment
            text = $"#{url.Authority}\n" + text;
            //and replace : from a host:port with an @
            var filteredAuthority = url.Authority.Replace(":", "@");
            foundCounter.Increment();
            File.WriteAllText($"{outputDir}{filteredAuthority}!robots.txt", text);
        }

    }
}
