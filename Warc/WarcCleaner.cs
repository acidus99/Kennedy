using System;

using Gemini.Net;
using WarcDotNet;

namespace Kennedy.Warc
{
    /// <summary>
    /// One off class to cleaning WARCs
    /// </summary>
	public static class WarcCleaner
    {
        public static void Fix(string inputWarc, string outputWarc)
		{
            DateTime prev = DateTime.Now;
            int Processed = 0;
            using (WarcWriter writer = new WarcWriter(outputWarc))
            {
                using (WarcReader reader = new WarcReader(inputWarc))
                {
                    foreach (var record in reader)
                    {
                        Processed++;
                        if (record is RequestRecord requestRecord)
                        {
                            //Clear any payload digest from a request
                            requestRecord.PayloadDigest = null;
                            FixRobotsRequestRecord(requestRecord);
                        }

                        if (record is ResponseRecord responseRecord)
                        {
                            FixRobotsResponseRecord(responseRecord);
                        }

                        //remove any block digest
                        if (record is WarcInfoRecord infoRecord)
                        {
                            infoRecord.BlockDigest = null;
                        }

                        writer.Write(record);
                    }
                }
            }

            int seconds = (int) DateTime.Now.Subtract(prev).TotalSeconds;
            if(seconds == 0)
            {
                seconds = 1;
            }

            Console.WriteLine($"Records: {Processed}\tTime: {seconds}s\tRate: {Processed / seconds} / s");
        }

        private static void UpdateBlockDigest(WarcRecord record)
        {
            if (record.ContentLength > 0)
            {
                record.BlockDigest = GeminiParser.GetStrongHash(record.ContentBlock!);
            }
        }
        
        private static void FixRobotsRequestRecord(RequestRecord requestRecord)
        {
            if(requestRecord.TargetUri == null)
            {
                return;
            }

            if (!requestRecord.TargetUri.AbsoluteUri.Contains("/robots.txt?kennedy-crawler"))
            {
                return;
            }

            string cleanUrl = requestRecord.TargetUri.AbsoluteUri.Replace("/robots.txt?kennedy-crawler", "/robots.txt");

            GeminiUrl geminiUrl = new GeminiUrl(cleanUrl);

            requestRecord.TargetUri = geminiUrl.Url;
            requestRecord.ContentBlock = GeminiParser.CreateRequestBytes(geminiUrl);
            UpdateBlockDigest(requestRecord);
        }

        private static void FixRobotsResponseRecord(ResponseRecord responseRecord)
        {
            if (responseRecord.TargetUri == null)
            {
                return;
            }

            if (!responseRecord.TargetUri.AbsoluteUri.Contains("/robots.txt?kennedy-crawler"))
            {
                return;
            }

            string cleanUrl = responseRecord.TargetUri.AbsoluteUri.Replace("/robots.txt?kennedy-crawler", "/robots.txt");

            GeminiUrl geminiUrl = new GeminiUrl(cleanUrl);
            responseRecord.TargetUri = geminiUrl.Url;
        }

        private static void FixResponsePayload(ResponseRecord responseRecord)
        {

            if(responseRecord.IdentifiedPayloadType != null && responseRecord.PayloadDigest != null)
            {
                //nothing to do here
                return;
            }

            if(responseRecord.TargetUri == null)
            {
                throw new ArgumentException("Target URI is null!");
            }

            if(responseRecord.ContentBlock == null)
            {
                throw new ArgumentException("Content Block is empty?!?!");
            }

            var geminiUrl = new GeminiUrl(responseRecord.TargetUri);
            var response = GeminiParser.ParseResponseBytes(geminiUrl, responseRecord.ContentBlock);

            //ensure a identified payload type exists
            if (responseRecord.IdentifiedPayloadType == null)
            {
                responseRecord.IdentifiedPayloadType = response.MimeType;
            }

            if(responseRecord.PayloadDigest == null && response.HasBody)
            {
                responseRecord.PayloadDigest = GeminiParser.GetStrongHash(response.BodyBytes!);
            }
        }

        //private static void OptimizeForStoage(ResponseRecord response)
        //{
        //    if(response.IdentifiedPayloadType == null)
        //    {
        //        return;
        //    }

        //    if (response.IdentifiedPayloadType.StartsWith("text/") || response.IdentifiedPayloadType.StartsWith("image/"))
        //    {
        //        return;
        //    }

        //    if (response.ContentLength > MaxUninterestingFileSize)
        //    {
        //        response.ContentBlock = response.ContentBlock!.Take(MaxUninterestingFileSize).ToArray();
        //        response.Truncated = "length";
        //          //TODO: Update the response digests!
        //    }
        //}


	}
}

