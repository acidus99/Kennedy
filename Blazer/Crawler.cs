using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using Gemini.Net;
using Kennedy.Blazer.Frontiers;
using Kennedy.Blazer.Processors;
using Kennedy.Blazer.Protocols;
using Kennedy.Blazer.Utils;

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

        IUrlFrontier UrlFrontier;

        List<IResponseProcessor> responseProcessors;

        SeenContentTracker seenContentTracker;


        public Crawler()
        {
            Directory.CreateDirectory(outputBase);
            errorOut = new ThreadedFileWriter(outputBase + "errors.txt", 1);
            logOut = new ThreadedFileWriter(outputBase + "log.tsv", 20);

            UrlFrontier = new CrawlQueue(5000);
            seenContentTracker = new SeenContentTracker();

            responseProcessors = new List<IResponseProcessor>
            {
                new RedirectProcessor(UrlFrontier),
                new GemtextProcessor(UrlFrontier)
            };
        }

        public void AddSeed(string url)
            => UrlFrontier.AddUrl(new GeminiUrl(url));

        private void LogError(Exception ex, GeminiUrl url)
        {
            var msg = $"EXCEPTION {ex.Message} on '{url}'";
            Console.WriteLine(msg);
            errorOut.WriteLine($"{DateTime.Now}\t{msg}");

            msg = $"XX\t{ex.Message}\t{url}\t0\t0";
            logOut.WriteLine(msg);
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
                url = UrlFrontier.GetUrl();
                if (url != null)
                {
                    Console.WriteLine($"Queue Len:{UrlFrontier.Count}\tRequesting '{url}'");

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
                            ProcessResponse(resp);
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

        private void ProcessResponse(GeminiResponse response)
        {
            if (!seenContentTracker.CheckAndRecord(response)) { }
            foreach (var processor in responseProcessors)
            {
                if (processor.CanProcessResponse(response))
                {
                    processor.ProcessResponse(response);
                }
            }
        }
    }

}
