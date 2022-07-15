using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Gemini.Net;
using Kennedy.Crawler.Utils;

namespace Kennedy.Crawler.Support
{

    /// <summary>
    /// Fetches Robots.txt for Gemini sites from a seed list of capsules
    ///
    /// robots.txt files will be dumped into an output folder with the format "[domain name]-robots.txt"
    /// 
    /// </summary>
    public static class RobotsFetcher
    {
        //folder to output robots.txt into
        static  readonly string outputDirRobots = $"/{CrawlerOptions.DataDirectory}/robots/";

        static ThreadSafeCounter requestCounter = new ThreadSafeCounter();
        static ThreadSafeCounter foundRobotsCounter = new ThreadSafeCounter();

        public static void DoIt(string domainsFile)
        {

            Directory.CreateDirectory(outputDirRobots);
            string[] domains = File.ReadAllLines(domainsFile);

            int total = domains.Length;

            int parallelThreadsCount = 10;

            Parallel.ForEach(domains, new ParallelOptions { MaxDegreeOfParallelism = parallelThreadsCount }, domain =>
            {
                int t = requestCounter.Increment();

                CheckRobot($"gemini://{domain}/robots.txt");

                Console.WriteLine($"Progress:\t{t}\t of {total}\tRobots Hits:\t{foundRobotsCounter.Count}");
            }); //close method invocation 
        }

        public static void CheckRobot(string surl)
        {
            GeminiUrl url = null;

            try
            {
                url = new GeminiUrl(surl);
            } catch(Exception ex)
            {
                return;
            }

            GeminiRequestor gemiRequestor = new GeminiRequestor();

            var resp = gemiRequestor.Request(url);

            if (gemiRequestor.LastException == null && IsValidRobotsResp(resp))
            {
                SaveFileRobot(url, resp.BodyText);
            }
        }

        private static bool IsValidRobotsResp(GeminiResponse resp)
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
        private static void SaveFileRobot(GeminiUrl url, string text)
        {

            foundRobotsCounter.Increment();
            //prepand the host/port in a comment
            text = $"#{url.Authority}\n" + text;
            //and replace : from a host:port with an @
            var filteredAuthority = url.Authority.Replace(":", "@");

            File.WriteAllText($"{outputDirRobots}{filteredAuthority}!robots.txt", text);
        }

    }
}
