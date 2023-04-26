using System;
using System.Collections.Specialized;

using Gemini.Net;


using Toimik.WarcProtocol;

namespace Kennedy.Warc
{
	public class GeminiWarcCreator :IDisposable
	{
		private WarcWriter writer;

		private GeminiRecordBuilder recordBuilder;

		private Uri? warcID = null;

		public GeminiWarcCreator(string outputFile)
		{
			writer = new WarcWriter(outputFile);
			recordBuilder = new GeminiRecordBuilder();
			WriteInfo();
		}

		private void WriteInfo()
		{
			NameValueCollection data = new NameValueCollection();
			data.Add("hostname", "kennedy.gemi.dev");
			data.Add("software", "Kennedy Gemini crawler");
			data.Add("timestamp", DateTime.Now.ToString());
			data.Add("operator", "Acidus");

			var warcinfo = recordBuilder.Warcinfo(data);
			warcID = warcinfo.Id;

			writer.Write(warcinfo);
		}

		public void RecordSession(GeminiResponse geminiResp)
			=> RecordSession(geminiResp.RequestSent, geminiResp.RequestUrl, geminiResp.ResponseReceived, geminiResp.StatusCode, geminiResp.Meta, geminiResp.BodyBytes);

		public void RecordSession(DateTime? requestSent, GeminiUrl requestUrl, DateTime? responseReceived, int statusCode, string meta, byte[] bodyBytes)
		{
			requestSent = requestSent ?? DateTime.Now;
			responseReceived = responseReceived ?? DateTime.Now;

            //create a request record
            var request = recordBuilder.RequestRecord(requestSent.Value, requestUrl, warcID!);
            writer.Write(request);

			//DateTime received, GeminiUrl requestUrl, int statusCode, string meta, byte [] bodyBytes, Uri warcID, Uri requestId)
            var response = recordBuilder.ResponseRecord(responseReceived.Value, requestUrl, statusCode, meta, bodyBytes, warcID!, request.Id);
            writer.Write(response);
        }

        public void RecordTruncatedSession(GeminiResponse geminiResp, string truncatedReason = "length")
			//uses MimeType, since the Meta contains the reason it was truncated, but Mime is still correct
            => RecordTruncatedSession(geminiResp.RequestSent, geminiResp.RequestUrl, geminiResp.ResponseReceived, geminiResp.StatusCode, geminiResp.MimeType, truncatedReason);

        public void RecordTruncatedSession(DateTime? requestSent, GeminiUrl requestUrl, DateTime? responseReceived, int statusCode, string mimeType, string truncatedReason = "length")
        {
            requestSent = requestSent ?? DateTime.Now;
            responseReceived = responseReceived ?? DateTime.Now;

            //create a request record
            var request = recordBuilder.RequestRecord(requestSent.Value, requestUrl, warcID!);
            writer.Write(request);

            var response = recordBuilder.ResponseRecord(responseReceived.Value, requestUrl, statusCode, mimeType, null, warcID!, request.Id, truncatedReason);
            writer.Write(response);
        }

        public void Dispose()
        {
            ((IDisposable)writer).Dispose();
        }
    }
}

