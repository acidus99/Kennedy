using System;
namespace Kennedy.Crawler.Filters
{
	public record class UrlFilterResult
	{
        public static readonly UrlFilterResult Allowed = new UrlFilterResult(true);

        public bool IsAllowed { get; private set; }

		public string Reason { get; private set; }

		public UrlFilterResult(bool isAllowed, string reason = "")
		{
			IsAllowed = isAllowed;
			Reason = reason;
		}
	}
}

