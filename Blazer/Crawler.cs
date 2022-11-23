using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using Gemini.Net;
using Kennedy.Blazer.Protocols;

namespace Kennedy.Blazer
{
    public class Crawler
    {
        readonly string outputBase = $"/var/gemini/{DateTime.Now.ToString("yyyy-MM-dd (mm)")}/";

        /// <summary>
        /// how long should we wait between requests
        /// </summary>
        const int delayMs = 1000;
        ThreadedFileWriter errorOut;
        ThreadedFileWriter logOut;


        CrawlQueue queue;
        DocumentStore docStore;

        public Crawler()
        {
            queue = new CrawlQueue(5000);

            Directory.CreateDirectory(outputBase);
            docStore = new DocumentStore(outputBase + "page-store/");
            errorOut = new ThreadedFileWriter(outputBase + "errors.txt", 1);
            logOut = new ThreadedFileWriter(outputBase + "log.tsv", 20);
        }

        public void AddSeed(string url)
            => queue.EnqueueUrl(new GeminiUrl(url));

        private void LogError(Exception ex, GeminiUrl url)
        {
            var msg = $"EXCEPTION {ex.Message} on '{url}'";
            Console.WriteLine(msg);
            errorOut.WriteLine($"{DateTime.Now}\t{msg}");

            msg = $"XX\t{ex.Message}\t{url}\t0\t0";
            logOut.WriteLine(msg);

        }

        private void LogWarn(string what)
        {
            var msg = $"WARNING! {what}";
            Console.WriteLine(msg);
            errorOut.WriteLine($"{DateTime.Now}\t{msg}");
        }


        private void CloseLogs()
        {
            errorOut.Close();
            logOut.Close();
        }

        private void LogPage(GeminiUrl url, GeminiResponse resp, int foundLinksCount)
        {

            var msg = $"{resp.StatusCode}\t{resp.MimeType}\t{url}\t{resp.BodySize}\t{foundLinksCount}";
            logOut.WriteLine(msg);
            Console.WriteLine($"\t{msg}");
        }

        public void DoCrawl()
        {
            var requestor = new GeminiProtocolHandler();

            GeminiUrl url = null;
            do
            {
                url = queue.DequeueUrl();
                if (url != null)
                {
                    Console.WriteLine($"Queue Len:{queue.Count}\tRequesting '{url}'");

                    var resp = requestor.Request(url);
                    //null means it was ignored by robots
                    if (resp != null)
                    {
                        if (resp.ConnectStatus != ConnectStatus.Success)
                        {
                            LogError(requestor.LastException, url);
                        }
                        else
                        {
                            var foundLinks = LinkFinder.ExtractLinks(resp);
                            queue.EnqueueUrls(foundLinks.Select(x => x.Url).ToList());
                            LogPage(url, resp, foundLinks.Count);

                            if (!docStore.Store(url, resp))
                            {
                                LogWarn($"Could not save document for '{url}' to disk");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Queue was empty...");
                }

                Thread.Sleep(delayMs);

            } while (url != null);
            Console.WriteLine("Complete!");
            CloseLogs();
            int x = 4;
        }

    }

}
