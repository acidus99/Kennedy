using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;

namespace Kennedy.CrawlData.Db
{
    [Table("Images")]
    public class StoredImageEntry
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
        
        public int Width { get; set; }

        public int Height { get; set; }

        public string ImageType { get; set; }

        public bool IsTransparent { get; set; }

        public void SetDocID()
        {
            DocID = DocumentIndex.toULong(DBDocID);
        }
    }
}
