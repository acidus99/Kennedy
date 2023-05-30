using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
namespace Kennedy.Data
{
	public class TlsConnectionInfo
	{
		public X509Certificate2? RemoteCertificate { get; set; }

		public SslProtocols? Protocol { get; set; }

		public TlsCipherSuite? CipherSuite { get; set; }
	}
}

