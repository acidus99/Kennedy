using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Gemi.Net;


namespace GemiCrawler.DocumentIndex.Db
{
    [Table("Documents")]
    public class StoredDocEntry
    {
        /// <summary>
        /// Just the autoincrement ID. not actually used by use
        /// </summary>
        [Key]
        public int Rowid { get; set; }


        /// <summary>
        /// the actual unique ID for a document/URL. We are using ulong
        /// since we are hashing our
        /// </summary>
        [NotMapped]
        public ulong DocID { get; set; }


        /// <summary>
        /// the ID we are using in the DB for the DocID. DocID is a ulong,
        /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
        /// some unchecked casting with overflow to handle it
        /// </summary>
        public long DBDocID { get; set; }

        public DateTime FirstSeen { get; set; }

        public DateTime? LastVisit { get; set; }

        [MaxLength(1024)]
        [Required]
        public string Url { get; set; }

        [Required]
        public string Domain { get; set; }

        [Required]
        public int Port { get; set; }

        public ConnectStatus ConnectStatus { get; set; } = ConnectStatus.Unknown;

        #region Things we get after fetching/parsing

        public int? Status { get; set; }

        /// <summary>
        /// everything after the status code
        /// </summary>
        public string MetaLine { get; set; }

        public int BodySize { get; set; }

        public string StorageKey { get; set; }

        public uint? BodyHash { get; set; }

        /// <summary>
        /// Latency of the request/resp, in ms
        /// </summary>
        public int ConnectTime { get; internal set; }

        public int DownloadTime { get; internal set; }

        #endregion

        #region Computed Fields that make it easier to query

        public string MimeType { get; set; }

        public string Language { get; set; }

        #endregion

        public long toLong(ulong ulongValue)
            => unchecked((long)ulongValue);

        public ulong toULong(long longValue)
            => unchecked((ulong)longValue);
    }
}
