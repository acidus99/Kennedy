using System;
using Warc;

using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Indexer.WarcProcessors
{
	public abstract class AbstractGeminiWarcProcessor : IWarcProcessor
    {
        public void ProcessRecord(WarcRecord record)
		{
            if (record.Type == "response")
            {
                var geminiResponse = GetGeminiResponse((record as ResponseRecord)!);
                if(geminiResponse != null)
                {
                    ProcessGeminiResponse(geminiResponse);
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

                var url = new GeminiUrl(StripRobots(responseRecord.TargetUri));

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

