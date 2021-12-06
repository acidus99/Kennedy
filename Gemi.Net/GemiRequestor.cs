using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using System.Linq;
using Gemi.Net.Utils;
using System.Diagnostics;


namespace Gemi.Net
{

    public class GemiRequestor
    {

        public Exception LastException;


        public GemiResponse Request(string url)
            => Request(new GemiUrl(url));

        public GemiResponse Request(GemiUrl url)
        {

            if (!url._url.IsAbsoluteUri)
            {
                throw new ApplicationException("Trying to request a non-absolute URL!");
            }

            var ret = new GemiResponse();
            var connectTimer = new Stopwatch();
            var downloadTimer = new Stopwatch();
            byte[] fullBytes = null;
            LastException = null;

            try
            {

                var sock = new TimeoutSocket();
                connectTimer.Start();
                var client = sock.Connect(url.Hostname, url.Port, 30000);

                using (SslStream sslStream = new SslStream(client.GetStream(), false,
                    new RemoteCertificateValidationCallback(ProcessServerCertificate), null))
                {

                    sslStream.ReadTimeout = 45000; //wait 15 sec
                    sslStream.AuthenticateAsClient(url.Hostname);
                    connectTimer.Stop();

                    sslStream.Write(MakeRequestBytes(url));
                    downloadTimer.Start();
                    
                    //TODO: probably shouldn't grab everything 
                    using (var ms = new MemoryStream())
                    {
                        sslStream.CopyTo(ms);
                        sslStream.Close();
                        downloadTimer.Stop();
                        fullBytes = ms.ToArray();
                    }

                    byte[] respLineBytes = fullBytes.TakeWhile(x => x != '\r').ToArray();

                    string respLine = Encoding.UTF8.GetString(respLineBytes);

                    byte[] bodyBytes = fullBytes.Skip(respLineBytes.Length + 2).ToArray();

                    ret = new GemiResponse(url, respLine)
                    {
                        ConnectTime = (int)connectTimer.ElapsedMilliseconds,
                        DownloadTime = (int)downloadTimer.ElapsedMilliseconds
                    };
                    ret.ParseBody(bodyBytes);
                }
                client.Close();
            } catch(Exception ex)
            {
                ret.ConnectStatus = ConnectStatus.Error;
                ret.ErrorMessage = ex.Message;
                LastException = ex;
            }
            return ret;
        }

        private bool ProcessServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            //TODO: TOFU logic and logic to store certificate that was received...
            return true;
        }

        private byte[] MakeRequestBytes(GemiUrl gurl)
            => Encoding.UTF8.GetBytes($"gemini://{gurl.Hostname}:{gurl.Port}{gurl.Path}\r\n");


    }
}
