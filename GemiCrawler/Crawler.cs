using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gemi.Net;
using System.Collections.Generic;
using System.Threading;


namespace GemiCrawler
{
    public class Crawler
    {
        readonly string outputBase = $"/Users/billy/Code/gemini-play/crawl-out/{DateTime.Now.ToString("yyyy-MM-dd (mm)")}/";

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
            => queue.EnqueueUrl(new GemiUrl(url));

        private void LogError(Exception ex, GemiUrl url)
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

        private void LogPage(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks)
        {

            var msg = $"{resp.StatusCode}\t{resp.MimeType}\t{url}\t{resp.SizeInfo()}\t{foundLinks.Count}";
            logOut.WriteLine(msg);
            Console.WriteLine($"\t{msg}");
        }

        public void DoCrawl()
        {


            var requestor = new GemiRequestor();

            GemiUrl url = null;
            do
            {
                url = queue.DequeueUrl();
                if (url != null)
                {
                    Console.WriteLine($"Queue Len:{queue.Count}\tRequesting '{url}'");

                    var resp = requestor.Request(url);

                    if (resp.ConnectStatus != ConnectStatus.Success)
                    {
                        LogError(requestor.LastException, url);
                        
                    }
                    else if (resp != null)
                    {
                        var foundLinks = LinkFinder.ExtractUrls(url, resp);
                        queue.EnqueueUrls(foundLinks);
                        LogPage(url, resp, foundLinks);
                        if(!docStore.Store(url, resp))
                        {
                            LogWarn($"Could not save document for '{url}' to disk");
                        }
                    }
                } else
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
