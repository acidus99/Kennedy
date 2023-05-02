using System;
namespace Kennedy.Data
{
	public class ServerInfo
	{
        public string Domain { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }

        public bool IsReachable { get; set; }
        public string? ErrorMessage { get; set; }

        public long? RobotsUrlID { get; set; }
        public long? FaviconUrlID { get; set; }
        public long? SecurityUrlID { get; set; }

        public string? FaviconTxt { get; set; }

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
    }
}
