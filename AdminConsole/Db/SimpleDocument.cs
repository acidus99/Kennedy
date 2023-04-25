﻿using System;
using Gemini.Net;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.AdminConsole.Db
{
    public class SimpleDocument
    {
        /// <summary>
        /// the ID we are using in the DB for the DocID. DocID is a ulong,
        /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
        /// some unchecked casting with overflow to handle it
        /// </summary>
        [Key]
        [Column("DBDocID")]
        public long UrlID { get; set; }

        public DateTime FirstSeen { get; set; }

        public int ErrorCount { get; set; } = 0;

        [MaxLength(1024)]
        [Required]
        public string Url { get; set; }

        private GeminiUrl? gUrl = null;

        [NotMapped]
        public GeminiUrl GeminiUrl
        {
            get
            {
                if (gUrl == null)
                {
                    gUrl = new GeminiUrl(Url);
                }
                return gUrl;
            }

        }

        [Required]
        public string Domain { get; set; }

        public ConnectStatus ConnectStatus { get; set; } = ConnectStatus.Unknown;

        public long TrueID
            => GeminiUrl.ID;

        [Required]
        public int Port { get; set; }

        public int? Status { get; set; }

        /// <summary>
        /// everything after the status code
        /// </summary>
        public string Meta { get; set; }

        public string? MimeType { get; set; }

        public bool BodySaved { get; set; } = false;

    }
}
