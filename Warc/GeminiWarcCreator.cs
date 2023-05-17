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
            : base(outputFile)
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

        public void WriteSession(GeminiResponse response)
        {
            var requestRecord = CreateRequestRecord(response.RequestUrl);
            if (response.RequestSent.HasValue)
            {
                requestRecord.Date = response.RequestSent.Value;
            }
            requestRecord.IpAddress = response.RemoteAddress?.ToString();

            Write(requestRecord);

            var responseRecord = new ResponseRecord
            {
                ContentBlock = GeminiParser.CreateResponseBytes(response),
                ContentType = ResponseContentType,
                WarcInfoId = WarcInfoID,
                TargetUri = response.RequestUrl.Url
            };
            if (response.ResponseReceived.HasValue)
            {
                responseRecord.Date = response.ResponseReceived.Value;
            }
            responseRecord.IpAddress = response.RemoteAddress?.ToString();

            responseRecord.ConcurrentTo.Add(requestRecord.Id);

            if (response.MimeType != null)
            {
                responseRecord.IdentifiedPayloadType = response.MimeType;
            }

            if (response.IsBodyTruncated)
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
                TargetUri = url.Url
            };

            responseRecord.ConcurrentTo.Add(requestRecord.Id);

            //do we have a mime?
            if (!string.IsNullOrEmpty(mime))
            {
                responseRecord.IdentifiedPayloadType = mime;
            }
            //was it truncated?
            if (isTruncated)
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
                TargetUri = url.Url
            };
    }
}
