using System;
namespace Kennedy.Data
{
	public class DomainInfo
	{
        public string Domain { get; set; }
        public int Port { get; set; }

        public bool IsReachable { get; set; }
        public string? ErrorMessage { get; set; }

        public long? RobotsUrlID { get; set; }
        public long? FaviconUrlID { get; set; }
        public long? SecurityUrlID { get; set; }

        public string? FaviconTxt { get; set; }
    }
}
