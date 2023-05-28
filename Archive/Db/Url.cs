using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;

namespace Kennedy.Archive.Db
{
	[Table("Urls")]
	[Index(nameof(Domain))]
	[Index(nameof(Port))]
    [Index(nameof(Protocol))]
    public class Url
	{
		[Key]
		public long Id { get; set; }

		public string FullUrl { get; set; } = "";

		public string Domain { get; set; } = "";

		public string Protocol { get; set; } = "";

		public int Port { get; set; } = 1965;

		public bool IsPublic { get; set; }

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
            Id = url.ID;
			FullUrl = url.NormalizedUrl;
			geminiUrl = url;
			Domain = url.Hostname;
			Port = url.Port;
			Protocol = url.Protocol;

			Snapshots = new List<Snapshot>();
		}
	}
}

