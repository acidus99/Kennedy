using System;
using System.Linq;
using Toimik.WarcProtocol;

using System.Text;

using Gemini.Net;

namespace Kennedy.AdminConsole.Warc
{
	public class GeminiRecordBuilder : RecordBuilder
	{
		public GeminiRecordBuilder(string version)
			: base(version)
		{
		}

        public RequestRecord RequestRecord(DateTime sent, GeminiUrl url, Uri warcID)
        {
			var requestString = $"{url}\r\n";
			return RequestRecord(sent, url._url, toBytes(requestString), "application/gemini; msgtype=request", warcID);
        }

        public ResponseRecord ResponseRecord(DateTime received, GeminiUrl requestUrl, int statusCode, string meta, byte [] bodyBytes, Uri warcID, Uri requestId, string? truncatedReason = null)
		{
			var responseLine = $"{statusCode} {meta}\r\n";

			byte[] fullResponseBytes = toBytes(responseLine);

			if(bodyBytes != null)
			{
				fullResponseBytes = fullResponseBytes.Concat(bodyBytes).ToArray();
			}

			return ResponseRecord(received, requestUrl._url, requestId, fullResponseBytes.ToArray(), "application/gemini; msgtype=response", warcID, truncatedReason);
		}

		private byte[] toBytes(string s)
			=> Encoding.UTF8.GetBytes(s);
    }
}

