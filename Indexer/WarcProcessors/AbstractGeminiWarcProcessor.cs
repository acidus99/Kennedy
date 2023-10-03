using System;
using WarcDotNet;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.Crawler.Filters;

namespace Kennedy.Indexer.WarcProcessors
{
	public abstract class AbstractGeminiWarcProcessor : IWarcProcessor
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        BlockListFilter denyFilter;
 
        public AbstractGeminiWarcProcessor(string configDirectory)
        {
            denyFilter = new BlockListFilter(configDirectory);
        }

        public void ProcessRecord(WarcRecord record)
		{
            if (record.Type == RecordType.Response)
            {
                var geminiResponse = GetGeminiResponse((record as ResponseRecord)!);
                if(geminiResponse != null)
                {
                    stopwatch.Restart();
                    ProcessGeminiResponse(geminiResponse);
                    stopwatch.Stop();
                }
            }
        }

        private GeminiResponse? GetGeminiResponse(ResponseRecord responseRecord)
        {
            if (responseRecord.TargetUri == null || responseRecord.ContentBlock == null)
            {
                return null;
            }

            try
            {
                bool isProactive = IsProactiveRequest(responseRecord);

                var url = new GeminiUrl(StripRobots(responseRecord.TargetUri));

                if (!isProactive)
                {
                    BlockResult result = denyFilter.IsUrlAllowed(url);

                    if (!result.IsAllowed)
                    {
                        //File.AppendAllText("/tmp/deny.txt", result.Reason + "\n");
                        return null;
                    }
                }

                var response = GeminiParser.ParseResponseBytes(url, responseRecord.ContentBlock);
                response.RequestSent = responseRecord.Date;
                response.ResponseReceived = responseRecord.Date;
                response.IsBodyTruncated = (responseRecord.Truncated?.Length > 0);
                return response;
            }
            catch (Exception)
            {
                Console.WriteLine("Malformed Gemini response record. Skipping");
                return null;
            }
        }

        protected bool IsProactiveRequest(ResponseRecord responseRecord)
        {
            return IsProactiveRequest(new GeminiUrl(responseRecord.TargetUri!));
        }

        protected bool IsProactiveRequest(GeminiUrl url)
        {
            if (url.Path == "/robots.txt" ||
                url.Path == "/favicon.txt" ||
                url.Path == "/.well-known/security.txt")
            {
                return true;
            }
            return false;
        }

        private Uri StripRobots(Uri url)
        {
            if (url.PathAndQuery == "/robots.txt?kennedy-crawler")
            {
                UriBuilder uriBuilder = new UriBuilder(url);
                uriBuilder.Query = "";
                return uriBuilder.Uri;
            }
            return url;
        }

        public abstract void FinalizeProcessing();

        protected abstract void ProcessGeminiResponse(GeminiResponse geminiResponse);
    }
}

