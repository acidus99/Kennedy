using System;
using WarcDotNet;

using Gemini.Net;
using Kennedy.Data.Utils;
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
                bool isProactive = UrlUtility.IsProactiveUrl(responseRecord.TargetUri);
                var url = new GeminiUrl(UrlUtility.RemoveCrawlerIdentifier(responseRecord.TargetUri));

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
            catch (Exception ex)
            {
                Console.WriteLine($"Malformed Gemini response record: {ex.Message}");
                return null;
            }
        }

        public abstract void FinalizeProcessing();

        protected abstract void ProcessGeminiResponse(GeminiResponse geminiResponse);
    }
}

