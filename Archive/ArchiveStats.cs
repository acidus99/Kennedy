using System;
namespace Kennedy.Archive
{
	public class ArchiveStats
	{
		/// <summary>
		/// Total number of domains which have archived content
		/// </summary>
		public long Domains { get; set; }

		/// <summary>
		/// Total number of URLs seen
		/// </summary>
		public long UrlsTotal
			=> UrlsPublic + UrlsExcluded;

		/// <summary>
		/// URLs that are included in the archive
		/// </summary>
		public long UrlsPublic { get; set; }

		/// <summary>
		/// URLs that have been excluded 
		/// </summary>
        public long UrlsExcluded { get; set; }

		/// <summary>
		/// Total number of captures in the archive
		/// </summary>
		public long Captures { get; set; }

		/// <summary>
		/// Total number of captures that contain unique content
		/// </summary>
		public long CapturesUnique { get; set; }

		/// <summary>
		/// Total size of archived content
		/// </summary>
		public long Size { get; set; }

		/// <summary>
		/// Total size if de-duplication was not used
		/// </summary>
		public long SizeWithoutDeDuplication { get; set; }


		public DateTime OldestSnapshot { get; set; }

		public DateTime NewestSnapshot { get; set; }
    }
}

