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
	public class Url
	{
		[Key]
		public long Id { get; set; }

		public string FullUrl { get; set; }

		public string Domain { get; set; }

		public int Port { get; set; }

		public string PackName { get; set; }

		public ICollection<Snapshot> Snapshots;

        [NotMapped]
        public GeminiUrl GeminiUrl
        {
            get
            {
                if (geminiUrl == null)
                {
                    geminiUrl = new GeminiUrl(FullUrl);
                }
                return geminiUrl;
            }
        }

        private GeminiUrl? geminiUrl = null;

        public Url()
        {
            Snapshots = new List<Snapshot>();
        }

		public Url(GeminiUrl url)
        {
            Id = unchecked((long)url.HashID);
			FullUrl = url.NormalizedUrl;
			geminiUrl = url;
			Domain = url.Hostname;
			Port = url.Port;

			Snapshots = new List<Snapshot>();
			PackName = Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(url.HashID))).ToLower();
		}
	}
}

