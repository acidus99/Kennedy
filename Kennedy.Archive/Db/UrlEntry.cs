using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;

using Gemini.Net;

namespace Kennedy.Archive.Db
{
	[Table("Urls")]
	[Index(nameof(Domain))]
	[Index(nameof(Port))]
	public class UrlEntry
	{
		[Key]
		public long UrlId { get; set; }

		public string Url { get; set; }

		public string Domain { get; set; }

		public int Port { get; set; }

		public string PackName { get; set; }

		public List<SnapshotEntry> Snapshots;

		public UrlEntry()
        {
        }

		public UrlEntry(GeminiUrl url)
        {
			UrlId = unchecked((long)url.HashID);
			Url = url.NormalizedUrl;
			Domain = url.Hostname;
			Port = url.Port;

			Snapshots = new List<SnapshotEntry>();
			PackName = Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(UrlId))).ToLower();
		}
	}
}

