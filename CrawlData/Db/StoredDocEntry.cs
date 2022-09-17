using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.Data.Models;

namespace Kennedy.CrawlData.Db
{
    [Table("Documents")]
    [Index(nameof(Status))]
    public class StoredDocEntry
    {
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
        [Key]
        public long DBDocID { get; set; }

        public DateTime FirstSeen { get; set; }

        public DateTime? LastVisit { get; set; }

        public DateTime? LastSuccessfulVisit { get; set; }

        public int ErrorCount { get; set; } = 0;


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
        public string Meta { get; set; }

        /// <summary>
        /// Did we deliberately skip downloading this body?
        /// </summary>
        public bool BodySkipped { get; set; } = false;

        public bool BodySaved { get; set; } = false;
        public int BodySize { get; set; }
        public uint? BodyHash { get; set; }

        public int OutboundLinks { get; set; }

        /// <summary>
        /// Latency of the request/resp, in ms
        /// </summary>
        public int ConnectTime { get; internal set; }

        public int DownloadTime { get; internal set; }

        #endregion

        #region Computed Fields that make it easier to query

        public string Title { get; set; }

        public string MimeType { get; set; }

        public string Language { get; set; }

        public int LineCount { get; set; }

        public double PopularityRank { get; set; }

        public int ExternalInboundLinks { get; set; }

        public ContentType ContentType { get; set; }

        #endregion

        public void SetDocID()
        {
            DocID = DocumentIndex.toULong(DBDocID);
        }
    }
}
