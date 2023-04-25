using System;
using System.Collections.Specialized;
using System.Text;

using Toimik.WarcProtocol;


namespace Kennedy.Warc
{
	public class RecordBuilder
	{

        public DigestFactory DigestFactory { get; }

        public PayloadTypeIdentifier PayloadTypeIdentifier { get; }


        public string Version { get; private set; }

		public RecordBuilder(string version)
		{
			Version = version;
            DigestFactory = new DigestFactory("sha1");
            PayloadTypeIdentifier = new PayloadTypeIdentifier();
        }

        private Uri CreateId()
        {
            var uri = new Uri($"urn:uuid:{Guid.NewGuid()}");
            return uri;
        }

        public WarcinfoRecord Warcinfo(NameValueCollection metaData = null)
		{
			return new WarcinfoRecord(Version, CreateId(), DateTime.Now, CreatePayload(metaData), "application/warc-fields");
		}

		public RequestRecord RequestRecord(DateTime sent, Uri targetUri, byte[] request, string contentType, Uri warcID)
		{
			return new RequestRecord(Version, CreateId(), sent, PayloadTypeIdentifier, request, contentType, warcID, targetUri);
		}

		public ResponseRecord ResponseRecord(DateTime received, Uri targetUri, Uri requestId, byte[] responseBytes, string contentType, Uri warcID, string? truncatedReason = null)
        {
			var response = new ResponseRecord(Version, CreateId(), received, PayloadTypeIdentifier, responseBytes, contentType, warcID, targetUri, digestFactory: DigestFactory, truncatedReason: truncatedReason);
			response.ConcurrentTos.Add(requestId);
			return response;
		}

		private string CreatePayload(NameValueCollection metaData)
		{
			if(metaData == null || !metaData.HasKeys())
			{
				return "";
			}

			StringBuilder sb = new StringBuilder();
			foreach(var key in metaData.AllKeys)
			{
				sb.AppendLine($"{key}: {metaData[key]}");
			}
			return sb.ToString();
		}
	}
}

