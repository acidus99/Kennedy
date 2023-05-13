using System;
namespace Kennedy.Data
{
	public class ServerInfo
	{
        public string Domain { get; set; } = "";
        public int Port { get; set; } = 1965;
        public string Protocol { get; set; } = "";
 
        public string Emoji { get; set; } = "";

        public int Pages { get; set; } = 0;
    }
}
