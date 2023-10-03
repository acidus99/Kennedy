using System;
namespace Kennedy.WarcConverters.MozzPortalImport
{
	public class WaybackSnapshot
	{
		public required string ContentType { get; set; }

		public required string OriginalUrl { get; set; }

		public required string Timestamp { get; set; }

		public string CaptureUrl
			=> $"https://web.archive.org/web/{Timestamp}if_/{OriginalUrl}";

        public DateTime DateTime
			=> DateTime.ParseExact(Timestamp, "yyyyMMddHHmmss", null);
	
	}
}

