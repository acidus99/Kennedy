using System;

using Gemini.Net;

namespace Kennedy.Data
{
	public class ParsedResponse : GeminiResponse
	{
		public ContentType ContentType { get; set; } = ContentType.Unknown;
		public List<FoundLink> Links { get; set; } = new List<FoundLink>();


		public ParsedResponse(GeminiResponse resp)
		{
			ConnectStatus = resp.ConnectStatus;

            BodyBytes = resp.BodyBytes;
			IsBodyTruncated = resp.IsBodyTruncated;
			BodyText = resp.BodyText;

			MimeType = resp.MimeType;
			Charset = resp.Charset;
			Meta = resp.Meta;

			ConnectTime = resp.ConnectTime;
			DownloadTime = resp.DownloadTime;

			RequestUrl = resp.RequestUrl;
			ResponseLine = resp.ResponseLine;

			StatusCode = resp.StatusCode;
		}

	}
}
