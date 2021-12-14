using System;
using System.IO;
using System.Threading.Tasks;
using Gemi.Net;
using GemiCrawler.Utils;


namespace GemiCrawler.Support
{
    public class Scanner
    {
        const string targets = "/Users/billy/Code/gemini-play/capsules.txt";


        const string outputDir = "/Users/billy/Code/gemini-play/capture/";


        public static void DoIt()
        {

            ThreadSafeCounter counter = new ThreadSafeCounter();
            ThreadedFileWriter logOut = new ThreadedFileWriter("/Users/billy/Code/gemini-play/capture-log.tsv", 1);



            string[] hosts = File.ReadAllLines(targets);

            int total = hosts.Length;

            int parallelThreadsCount = 40;

            Parallel.ForEach(hosts, new ParallelOptions { MaxDegreeOfParallelism = parallelThreadsCount }, host =>
            {
                int t = counter.Increment();

                GemiRequestor gemiRequestor = new GemiRequestor();

                var fullUrl = $"gemini://{host}/";

                var resp = gemiRequestor.Request(fullUrl);

                var outLine = "";

                if(gemiRequestor.LastException != null)
                {
                    outLine = $"EXCEPTION {gemiRequestor.LastException.Message} on '{host}'";
                } else if (resp != null)
                {
                    outLine = $"{t}\t{fullUrl}\t{resp.ResponseLine}\t{resp.BodySize}";
                    SaveFile(host, resp);
                }

                Console.WriteLine(outLine);
                logOut.WriteLine(outLine);
            }); //close method invocation 
            logOut.Close();

            int xxx = 5;



        }

        private static void SaveFile(string host, GemiResponse resp)
        {
            if(resp != null && resp.IsTextResponse)
            {
                File.WriteAllText($"{outputDir}{host}.gmi", resp.BodyText);
            }
        }

    }
}
