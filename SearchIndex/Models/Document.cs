using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.SearchIndex.Models
{
    [Table("Documents")]
    [Index(nameof(StatusCode))]
    [Index(nameof(Domain))]
    public class Document
    {
        /// <summary>
        /// the ID we are using in the DB for the DocID. DocID is a ulong,
        /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
        /// some unchecked casting with overflow to handle it
        /// </summary>
        [Key]
        public long UrlID { get; set; }

        /// <summary>
        /// When was the record first seen
        /// </summary>
        public DateTime FirstSeen { get; set; }

        /// <summary>
        /// When did we last visit this document
        /// </summary>
        public DateTime LastVisit { get; set; }

        /// <summary>
        /// When did we last successfully visit this document?
        /// </summary>
        public DateTime? LastSuccessfulVisit { get; set; }

        [MaxLength(1024)]
        [Required]
        public string Url { get; set; }

        [Required]
        public string Protocol { get; set; }

        [Required]
        public string Domain { get; set; }

        [Required]
        public int Port { get; set; }

        [Required]
        public string Path { get; set; }

        [Required]
        public string FileExtension { get; set; }

        [NotMapped]
        public GeminiUrl GeminiUrl
        {
            get
            {
                if (geminiUrl == null)
                {
                    geminiUrl = new GeminiUrl(Url);
                }
                return geminiUrl;
            }
        }

        private GeminiUrl geminiUrl = null;

        public bool IsAvailable { get; set; }

        #region Things we get after fetching/parsing

        [Required]
        public int StatusCode { get; set; }

        /// <summary>
        /// everything after the status code
        /// </summary>
        public string Meta { get; set; }

        /// <summary>
        /// Do we not have the entire body?
        /// </summary>
        public bool IsBodyTruncated { get; set; } = false;

        public int BodySize { get; set; }
        public long? BodyHash { get; set; }

        public long ResponseHash { get; set; }

        public int OutboundLinks { get; set; }

        #endregion

        #region Computed Fields that make it easier to query

        public string? Title { get; set; }

        public string? MimeType { get; set; }

        /// <summary>
        /// Charset parsed out of the Meta field
        /// </summary>
        public string? Charset { get; set; }

        /// <summary>
        /// 2 letter ISO Language code parsed out of the Meta field
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// 2 letter ISO Language code we detected for this, if any
        /// </summary>
        public string? DetectedLanguage { get; set; }

        public int LineCount { get; set; }

        public double PopularityRank { get; set; }

        public int ExternalInboundLinks { get; set; }

        public ContentType ContentType { get; set; }

        #endregion

        public Document()
        {
        }

        public Document(GeminiUrl url)
        {
            UrlID = url.ID;
            Protocol = url.Protocol;
            Domain = url.Hostname;
            Port = url.Port;
            Path = url.Path;
            Url = url.NormalizedUrl;
            FileExtension = url.FileExtension;
        }

    }
}
