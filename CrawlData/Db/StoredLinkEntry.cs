using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Gemini.Net;


namespace Kennedy.CrawlData.Db
{
    [Table("Links")]
    public class StoredLinkEntry
    {
        /// <summary>
        /// the ID we are using in the DB for the DocID. DocID is a ulong,
        /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
        /// some unchecked casting with overflow to handle it
        /// </summary>
        public long DBSourceDocID { get; set; }
        public long DBTargetDocID { get; set; }

        public bool IsExternal { get; set; }
        public string LinkText { get; set; }

    }
}
