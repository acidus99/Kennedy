using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Gemi.Net;


namespace GemiCrawler.MetaStore.Db
{
    [Table("StoreResponse")]
    public class StoredResponse
    {
        [Key]
        public int Id { get; set; }

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

    }
}
