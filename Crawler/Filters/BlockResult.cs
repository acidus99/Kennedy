using System;
namespace Kennedy.Crawler.Filters
{
	public record class BlockResult
	{
        public static readonly BlockResult Allowed = new BlockResult(true);

        public bool IsAllowed { get; private set; }

		public string Reason { get; private set; }

		public BlockResult(bool isAllowed, string reason = "")
		{
			IsAllowed = isAllowed;
			Reason = reason;
		}
	}
}

