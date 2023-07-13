using System;
namespace Kennedy.Crawler.Filters
{
	public record class BlockResult
	{
        public static readonly BlockResult Allowed = new BlockResult(true);

        public bool IsAllowed { get; private set; }

		public string RejectionType { get; private set; }

		public string SpecificRule { get; private set; }

		public BlockResult(bool isAllowed, string rejectionType="", string specificRule ="")
		{
			IsAllowed = isAllowed;
			RejectionType = rejectionType;
			SpecificRule = specificRule;
		}
	}
}

