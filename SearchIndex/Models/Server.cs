using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Gemini.Net;


namespace Kennedy.SearchIndex.Models
{
    [Table("Server")]
    [PrimaryKey(nameof(Domain), nameof(Port), nameof(Protocol))]
    public class Server
    {
        public string Domain { get; set; }

        public int Port { get; set; }

        public string Protocol { get; set; }

        public bool IsReachable { get; set; }
        public string? ErrorMessage { get; set; }

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

        [NotMapped]
        public bool HasSecurityTxt
            => (SecurityUrlID != null);

        [NotMapped]
        public bool HasRobotsTxt
            => (RobotsUrlID != null);

        public long? RobotsUrlID { get; set; }
        public long? FaviconUrlID { get; set; }
        public long? SecurityUrlID { get; set; }
        
        public string? FaviconTxt { get; set; }
    }
}
