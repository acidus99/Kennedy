using System;

using Gemini.Net;
using Warc;

namespace Kennedy.Warc
{
    /// <summary>
    /// One off class to rewrite WARCs and truncate them
    /// </summary>
	public static class WarcTruncater
    {
        const int MaxUninterestingFileSize = 1024 * 1024 + 2000;

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
                        if(reader.RecordsRead % 100 == 0)
                        {
                            Console.WriteLine(reader.RecordsRead);
                        }

                        if (record is RequestRecord)
                        {
                            FixBlockDigest(record);
                        }

                        if (record is ResponseRecord responseRecord)
                        {
                            FixBlockDigest(record);
                            FixResponsePayload(responseRecord);
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


        private static void FixBlockDigest(WarcRecord record)
        {
            if (record.ContentLength > 0)
            {
                record.BlockDigest = GeminiParser.GetStrongHash(record.ContentBlock!);
            }
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

