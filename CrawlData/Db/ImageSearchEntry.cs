using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace Kennedy.CrawlData.Db
{
	[Table("ImageSearch")]
	public class ImageSearchEntry
	{
		[Key]
		public long ROWID { get; set; }
		public string Terms { get; set; }
	}
}

