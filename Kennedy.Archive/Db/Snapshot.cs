using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

using Kennedy.Data;
using Gemini.Net;

namespace Kennedy.Archive.Db
{
	[Table("Snapshots")]
	[Index(nameof(DataHash))]
	public class Snapshot
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		public long Offset { get; set; }
		public long Size { get; set; }

		public int StatusCode { get; set; }

		public long DataHash { get; set; }

		public ContentType ContentType { get; set; }

		public DateTime Captured { get; set; }

        public string Meta { get; set; }

        public long UrlId { get; set; }

        public Url Url { get; set; }

		public bool IsGemtext
			=> (ContentType == ContentType.Text) && Meta.StartsWith("text/gemini");

    }
	
}

