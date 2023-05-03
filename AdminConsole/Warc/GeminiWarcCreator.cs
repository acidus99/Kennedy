using System;
using System.Collections.Specialized;

using Gemini.Net;


using Toimik.WarcProtocol;

namespace Kennedy.AdminConsole.Warc
{
	public class GeminiWarcCreator :IDisposable
	{
		private WarcWriter writer;

		private GeminiRecordBuilder recordBuilder;

		private Uri? warcID = null;

		public GeminiWarcCreator(string outputFile)
		{
			writer = new WarcWriter(outputFile);
			recordBuilder = new GeminiRecordBuilder("1.1");
			WriteInfo();
		}

		private void WriteInfo()
		{
			NameValueCollection data = new NameValueCollection();
			data.Add("hostname", "kennedy.gemi.dev");
			data.Add("software", "Kennedy Gemini crawler");
			data.Add("operator", "Acidus");

			var warcinfo = recordBuilder.Warcinfo(data);
			warcID = warcinfo.Id;

			writer.WriteRecord(warcinfo);
		}

		public void RecordSession(DateTime sent, GeminiResponse geminiResp)
			=> RecordSession(sent, geminiResp.RequestUrl, geminiResp.StatusCode, geminiResp.Meta, geminiResp.BodyBytes);

		public void RecordSession(DateTime sent, GeminiUrl requestUrl, int statusCode, string meta, byte[] bodyBytes)
		{
            //create a request record
            var request = recordBuilder.RequestRecord(sent, requestUrl, warcID);
            writer.WriteRecord(request);

			//DateTime received, GeminiUrl requestUrl, int statusCode, string meta, byte [] bodyBytes, Uri warcID, Uri requestId)
            var response = recordBuilder.ResponseRecord(sent.AddSeconds(2), requestUrl, statusCode, meta, bodyBytes, warcID, request.Id);
            writer.WriteRecord(response);
        }

        public void RecordTruncatedSession(DateTime sent, GeminiUrl requestUrl, int statusCode, string meta, string truncatedReason = "length")
        {
            //create a request record
            var request = recordBuilder.RequestRecord(sent, requestUrl, warcID);
            writer.WriteRecord(request);

            //DateTime received, GeminiUrl requestUrl, int statusCode, string meta, byte [] bodyBytes, Uri warcID, Uri requestId)
            var response = recordBuilder.ResponseRecord(sent.AddSeconds(2), requestUrl, statusCode, meta, null, warcID, request.Id, truncatedReason);
            writer.WriteRecord(response);
        }

        public void Dispose()
        {
            ((IDisposable)writer).Dispose();
        }
    }
}

