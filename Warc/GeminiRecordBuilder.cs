using System;
using System.Linq;
using Toimik.WarcProtocol;

using System.Text;

using Gemini.Net;

using System.Collections.Specialized;

namespace Kennedy.Warc
{
    /// <summary>
    /// Builds WARC records for Gemini requests/responses
    /// </summary>
	public class GeminiRecordBuilder
	{

        DigestFactory DigestFactory { get; }

        PayloadTypeIdentifier PayloadTypeIdentifier { get; }

        // Always use 1.1
        const string Version = "1.1";

        public GeminiRecordBuilder()
        {
            //use sha1 for now
            DigestFactory = new DigestFactory("sha1");

            //use a generic, empty identifier for now, until
            //WarcProtocol supports better identification of payloads
            PayloadTypeIdentifier = new PayloadTypeIdentifier();
        }

        public WarcinfoRecord Warcinfo(NameValueCollection metaData = null)
        {
            return new WarcinfoRecord(Version, CreateId(), DateTime.Now, CreatePayload(metaData), "application/warc-fields");
        }


        private Uri CreateId()
            => new Uri($"urn:uuid:{Guid.NewGuid()}");

        private string CreatePayload(NameValueCollection metaData)
        {
            if (metaData == null || !metaData.HasKeys())
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            foreach (var key in metaData.AllKeys)
            {
                sb.AppendLine($"{key}: {metaData[key]}");
            }
            return sb.ToString();
        }

        public RequestRecord RequestRecord(DateTime timestamp, GeminiUrl url, Uri warcID)
        {
            return new RequestRecord(Version,
                                    CreateId(),
                                    timestamp,
                                    PayloadTypeIdentifier,
                                    toBytes($"{url}\r\n"),
                                    "application/gemini; msgtype=request",
                                    warcID,
                                    url._url);
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

        public ResponseRecord ResponseRecord(DateTime received, Uri targetUri, Uri requestId, byte[] responseBytes, string contentType, Uri warcID, string? truncatedReason = null)
        {
            var response = new ResponseRecord(Version, CreateId(), received, PayloadTypeIdentifier, responseBytes, contentType, warcID, targetUri, digestFactory: DigestFactory, truncatedReason: truncatedReason);
            response.ConcurrentTos.Add(requestId);
            return response;
        }

        private byte[] toBytes(string s)
			=> Encoding.UTF8.GetBytes(s);
    }
}
