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

            ThreadedFileWriter logOut = new ThreadedFileWriter($"{Crawler.DataDirectory}/vuln.csv", 1);

            string[] hosts = File.ReadAllLines($"{Crawler.DataDirectory}caps.txt");

            int total = hosts.Length;

            int parallelThreadsCount = 10;
            Console.WriteLine($"Starting...");
            Parallel.ForEach(hosts, new ParallelOptions { MaxDegreeOfParallelism = parallelThreadsCount }, host =>
            {
                int t = counter.Increment();
                if(IsVuln(host))
                {
                    found.Increment();
                    var line = $"{host}, gemini://{host}/%2F/, vulnerable!";
                    Console.WriteLine(line);
                    logOut.WriteLine(line);
                }
                Console.WriteLine($"{t}\t{total}\t{found.Count}");
            }); //close method invocation 
            logOut.Close();

            int xxx = 5;

        }

        private static bool IsVuln(string host)
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
    }
}
