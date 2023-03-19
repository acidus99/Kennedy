using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Gemini.Net;

namespace ArchiveLoader
{
    [Table("Documents")]
    public class SimpleDocEntry
    {
        /// <summary>
        /// the actual unique ID for a document/URL. We are using ulong
        /// since we are hashing our
        /// </summary>
        [NotMapped]
        public ulong DocID 
            => unchecked((ulong)DBDocID);

        /// <summary>
        /// the ID we are using in the DB for the DocID. DocID is a ulong,
        /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
        /// some unchecked casting with overflow to handle it
        /// </summary>
        [Key]
        public long DBDocID { get; set; }

        public DateTime FirstSeen { get; set; }

        public int ErrorCount { get; set; } = 0;

        [MaxLength(1024)]
        [Required]
        public string Url { get; set; }

        [Required]
        public string Domain { get; set; }


        public long TrueID()
        {
            GeminiUrl url = new GeminiUrl(Url);
            return unchecked((long)url.HashID);
        }

        [Required]
        public int Port { get; set; }

        public int? Status { get; set; }

        /// <summary>
        /// everything after the status code
        /// </summary>
        public string Meta { get; set; }

        public bool BodySaved { get; set; } = false;
        
    }
}
