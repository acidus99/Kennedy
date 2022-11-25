using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using Gemini.Net;
using Kennedy.Blazer.Frontiers;
using Kennedy.Blazer.Logging;
using Kennedy.Blazer.Processors;
using Kennedy.Blazer.Protocols;
using Kennedy.Blazer.Utils;

namespace Kennedy.Blazer
{
    public class Crawler
    {
        /// <summary>
        /// how long should we wait between requests
        /// </summary>
        const int delayMs = 1000;

        ErrorLog errorLog;

        IUrlFrontier UrlFrontier;
        UrlFrontierWrapper FrontierWrapper;

        List<IResponseProcessor> responseProcessors;

        SeenContentTracker seenContentTracker;

        public Crawler()
        {
            ConfigureDirectories();

            UrlFrontier = new CrawlQueue(5000);
            FrontierWrapper = new UrlFrontierWrapper(UrlFrontier);

            seenContentTracker = new SeenContentTracker();

            responseProcessors = new List<IResponseProcessor>
            {
                new RedirectProcessor(FrontierWrapper),
                new GemtextProcessor(FrontierWrapper)
            };
        }

        private void ConfigureDirectories()
        {
            Directory.CreateDirectory(CrawlerOptions.OutputBase);
            errorLog = new ErrorLog(CrawlerOptions.ErrorLog);
        }

        public void AddSeed(string url)
            => UrlFrontier.AddUrl(new GeminiUrl(url));

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
                            var msg = requestor.LastException?.Message ?? resp.Meta;
                            errorLog.LogError(msg, url.NormalizedUrl);
                        }
                        else
                        {
                            ProcessResponse(resp);
                        }
                    }
                }

                Thread.Sleep(delayMs);

            } while (url != null);
            Console.WriteLine("Complete!");
        }

        private void ProcessResponse(GeminiResponse response)
        {
            if (!seenContentTracker.CheckAndRecord(response))
            {
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

}
