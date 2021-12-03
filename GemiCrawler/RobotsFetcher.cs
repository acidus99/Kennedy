using System;
using System.IO;
using System.Linq;
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
        //list of domains
        const string targets = "/Users/billy/Code/gemini-play/capsules-to-scan.txt";

        //folder to output robots.txt into
        const string outputDir = "/Users/billy/Code/gemini-play/robots/";


        static ThreadSafeCounter requestCounter = new ThreadSafeCounter();
        static ThreadSafeCounter foundCounter = new ThreadSafeCounter();


        public static void DoIt()
        {

            string[] hosts = File.ReadAllLines(targets);

            int total = hosts.Length;

            int parallelThreadsCount = 16;

            Parallel.ForEach(hosts, new ParallelOptions { MaxDegreeOfParallelism = parallelThreadsCount }, host =>
            {
                int t = requestCounter.Increment();

                GemiRequestor gemiRequestor = new GemiRequestor();

                var fullUrl = $"gemini://{host}/robots.txt";

                var resp = gemiRequestor.Request(fullUrl);

                if(gemiRequestor.LastException == null && IsValidRobotsResp(resp))
                {
                    SaveFile(host, resp.BodyText);
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

        private static void SaveFile(string host, string text)
        {
            int count = foundCounter.Increment();
            Console.WriteLine($"\tHIT on Host '{host}'");
            File.WriteAllText($"{outputDir}{host}-robots.txt", text);
        }

    }
}
