using System;

using Gemini.Net;

namespace Kennedy.Data
{
	public class ParsedResponse : GeminiResponse
	{
		public ContentType ContentType { get; set; } = ContentType.Unknown;

		public List<FoundLink> Links { get; set; }

		public ParsedResponse(GeminiResponse baseResponse)
			: base(baseResponse.RequestUrl)
		{
			Links = new List<FoundLink>();

            StatusCode = baseResponse.StatusCode;
            Meta = baseResponse.Meta;
			RemoteAddress = baseResponse.RemoteAddress;
			RequestSent = baseResponse.RequestSent;
			ResponseReceived = baseResponse.ResponseReceived;

			//body properties
            BodyBytes = baseResponse.BodyBytes;
			IsBodyTruncated = baseResponse.IsBodyTruncated;

			//parsed items if there is a body
			MimeType = baseResponse.MimeType;
			Charset = baseResponse.Charset;
			Language = baseResponse.Language;

			//timers
			ConnectTime = baseResponse.ConnectTime;
			DownloadTime = baseResponse.DownloadTime;
		}
	}
}
