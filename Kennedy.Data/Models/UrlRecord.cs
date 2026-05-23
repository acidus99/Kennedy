using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Models;

    /// <summary>
    /// Registry of all URLs the crawler has ever seen, with
    /// visit history and high-level status for scheduling / policy.
    /// </summary>
    [Table("UrlRegistry")]
    [Index(nameof(NormalizedUrl), IsUnique = true)]
    [Index(nameof(Status), nameof(PriorityScore))]
    public class UrlRecord
    {
        public UrlRecord()
        {
        }

        public UrlRecord(string normalizedUrl)
        {
            NormalizedUrl = normalizedUrl;
            FirstSeen = DateTime.UtcNow;
            Status = UrlStatus.New;
        }

        /// <summary>
        /// Numeric primary key for this URL (SQLite INTEGER PRIMARY KEY).
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Canonical, normalized URL string.
        /// This should be the same normalized form you already use in Document.Url.
        /// </summary>
        [MaxLength(1024)]
        [Required]
        public string NormalizedUrl { get; set; } = "";

        /// <summary>
        /// When we first learned this URL existed (UTC).
        /// </summary>
        [Required]
        public DateTime FirstSeen { get; set; }

        /// <summary>
        /// Last time we attempted to visit this URL (success or failure) (UTC).
        /// Null if we've never tried.
        /// </summary>
        public DateTime? LastVisit { get; set; }

        /// <summary>
        /// Last time we successfully fetched indexable content (UTC).
        /// </summary>
        public DateTime? LastSuccess { get; set; }

        /// <summary>
        /// Last time the content hash changed (UTC).
        /// This only moves when LastContentHash changes.
        /// </summary>
        public DateTime? LastContentChange { get; set; }

        /// <summary>
        /// Hash of the last successfully fetched content
        /// (e.g. SHA-256 hex of normalized body).
        /// Null if we never had a successful fetch.
        /// </summary>
        public string? LastContentHash { get; set; }

        /// <summary>
        /// Raw protocol status from the last fetch attempt (20, 30, 51, 404, 500, etc.).
        /// Null when we never reached protocol level (DNS/TLS/etc.).
        /// </summary>
        public int? LastStatusCode { get; set; }

        /// <summary>
        /// High-level semantic state for scheduling, indexing, and policy.
        /// </summary>
        [Required]
        public UrlStatus Status { get; set; } = UrlStatus.New;

        /// <summary>
        /// How many successful, indexable fetches we've seen for this URL.
        /// </summary>
        public int SuccessCount { get; set; } = 0;

        /// <summary>
        /// How many failed attempts (TemporaryError, ConnectionError, PermanentError, etc.).
        /// </summary>
        public int FailureCount { get; set; } = 0;

        /// <summary>
        /// Scheduler priority / desirability: higher means "crawl sooner".
        /// You can set this from your unified events log.
        /// </summary>
        public double PriorityScore { get; set; } = 0.0;

        /// <summary>
        /// Used for redirects and interactive prompts
        /// </summary>
        public string Meta { get; set; } = "";

    }
