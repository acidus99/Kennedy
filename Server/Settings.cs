using System;
namespace Kennedy.Server
{
    public class Settings
    {
        public static Settings Global = null;

        public string Host { get; set; }
        public int Port { get; set; }
        public string CertificateFile { get; set; }
        public string KeyFile { get; set; }
        public string PublicRoot { get; set; }
        public string DataRoot { get; set; }
    }
}
