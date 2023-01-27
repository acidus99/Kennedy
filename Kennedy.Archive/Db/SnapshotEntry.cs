using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

using Kennedy.Data.Models;

namespace Kennedy.Archive.Db
{
	[Table("Snapshots")]
	[Index(nameof(DataHash))]
	public class SnapshotEntry
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long SnapshotId { get; set; }

		public long Offset { get; set; }
		public long Size { get; set; }

		public int StatusCode { get; set; }

		public long DataHash { get; set; }

		public ContentType ContentType { get; set; }

		public DateTime Captured { get; set; }

		public UrlEntry UrlEntry { get; set; }
	}
}

