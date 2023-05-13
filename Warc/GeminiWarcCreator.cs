using System;
using System.Collections.Specialized;
using System.Text;

using Gemini.Net;

using Warc;

namespace Kennedy.Warc
{
	public class GeminiWarcCreator : WarcWriter
	{
        public const string RequestContentType = "application/gemini; msgtype=request";
        public const string ResponseContentType = "application/gemini; msgtype=response";

        public Uri WarcInfoID { get; private set; }

		public GeminiWarcCreator(string outputFile)
            :base(outputFile)
		{
            WarcInfoID = WarcRecord.CreateId();
		}

        public void WriteWarcInfo(WarcFields fields)
        {
            Write(new WarcInfoRecord
            {
                Id = WarcInfoID,
                ContentType = WarcFields.ContentType,
                ContentText = fields.ToString()
            });
        }

        public void WriteSession(GeminiResponse geminiResp)
        {
            var requestRecord = CreateRequestRecord(geminiResp.RequestUrl);
            requestRecord.Date = geminiResp.RequestSent;

            Write(requestRecord);

            var responseRecord = new ResponseRecord
            {
                ContentBlock = GeminiParser.CreateResponseBytes(geminiResp),
                ContentType = ResponseContentType,
                Date = geminiResp.RequestSent,
                WarcInfoId = WarcInfoID,
                TargetUri = geminiResp.RequestUrl._url
            };

            responseRecord.ConcurrentTo.Add(requestRecord.Id);

            if (geminiResp.MimeType != null)
            {
                responseRecord.IdentifiedPayloadType = geminiResp.MimeType;
            }

            if(geminiResp.IsBodyTruncated)
            {
                responseRecord.Truncated = "length";
            }

            Write(responseRecord);
        }

        public void WriteLegacySession(GeminiUrl url, DateTime sent, int statusCode, string meta, string mime, byte[]? bytes, bool isTruncated = false)
        {

            var requestRecord = CreateRequestRecord(url);
            requestRecord.Date = sent;

            Write(requestRecord);

            var responseRecord = new ResponseRecord
            {
                ContentBlock = GeminiParser.CreateResponseBytes(statusCode, meta, bytes),
                ContentType = ResponseContentType,
                Date = sent,
                WarcInfoId = WarcInfoID,
                TargetUri = url._url
            };

            responseRecord.ConcurrentTo.Add(requestRecord.Id);

            //do we have a mime?
            if(!string.IsNullOrEmpty(mime))
            {
                responseRecord.IdentifiedPayloadType = mime;
            }
            //was it truncated?
            if(isTruncated)
            {
                responseRecord.Truncated = "length";
            }

            Write(responseRecord);
        }

        public RequestRecord CreateRequestRecord(GeminiUrl url)
            => new RequestRecord
            {
                ContentBlock = GeminiParser.CreateRequestBytes(url),
                ContentType = RequestContentType,
                WarcInfoId = WarcInfoID,
                TargetUri = url._url
            };      
    }
}
