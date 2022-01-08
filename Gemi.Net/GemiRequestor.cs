using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using System.Linq;
using Gemi.Net.Utils;
using System.Diagnostics;
using System.Collections.Generic;


//Using aspects of Gemini C# library SmolNetSharp for inspiration specifically:
//https://github.com/LukeEmmet/SmolNetSharp/blob/master/SmolNetSharp/Gemini.cs
// - Reading in the response line and parsing it before reading in the body
// - using a timeout and max response size to abort out early
// things I added:
// - Deciding to download the body or not based on the MIME type. This allows crawlers
// that are only interested in text content to move on more quickly and use less server
// resources
namespace Gemi.Net
{

    public class GemiRequestor
    {
        const int ResponseLineMaxLen = 1100;

        public Exception LastException;

        Stopwatch ConnectTimer;
        Stopwatch DownloadTimer;
        Stopwatch AbortTimer;

        public bool OnlyDownloadText { get; set; } = false;

        /// <summary>
        /// Amount of time, in ms, to wait before aborting the request or download
        /// </summary>
        public int AbortTimeout { get; set; } = 30 * 1000;

        /// <summary>
        /// Maximum amount of data to download for the response body, before aborting
        /// </summary>
        public int MaxResponseSize { get; set; } = 5 * 1024 * 1024;

        public GemiResponse Request(string url)
            => Request(new GemiUrl(url));

        public GemiResponse Request(GemiUrl url)
        {

            if (!url._url.IsAbsoluteUri)
            {
                throw new ApplicationException("Trying to request a non-absolute URL!");
            }

            var ret = new GemiResponse(url);

            AbortTimer = new Stopwatch();
            ConnectTimer = new Stopwatch();
            DownloadTimer =  new Stopwatch();
            LastException = null;

            try
            {
                var sock = new TimeoutSocket();
                AbortTimer.Start();
                ConnectTimer.Start();
                var client = sock.Connect(url.Hostname, url.Port, 60000);

                using (SslStream sslStream = new SslStream(client.GetStream(), false,
                    new RemoteCertificateValidationCallback(ProcessServerCertificate), null))
                {

                    sslStream.ReadTimeout = 60000; //wait 45 sec
                    sslStream.AuthenticateAsClient(url.Hostname);
                    ConnectTimer.Stop();

                    sslStream.Write(MakeRequestBytes(url));
                    DownloadTimer.Start();

                    ret = ReadResponseLine(sslStream, url);
                    ret.ConnectTime = (int)ConnectTimer.ElapsedMilliseconds;

                    //We don't need to download the body if we don't like the mime type
                    if (ShouldDownloadBody(ret))
                    {
                        var bodyBytes = ReadBody(sslStream);

                        ret.DownloadTime = (int)DownloadTimer.ElapsedMilliseconds;
                        ret.ParseBody(bodyBytes);
                    }
                }
                client.Close();
            } catch(Exception ex)
            {
                ret.ConnectStatus = ConnectStatus.Error;
                ret.Meta = ex.Message;
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

        private GemiResponse ReadResponseLine(Stream stream, GemiUrl url)
        {
            var respLineBuffer = new List<byte>(ResponseLineMaxLen);
            byte[] readBuffer = { 0 };

            int readCount = 0;
            //the response line is at most (2 + 1 + 1024 + 2) characters long. (a redirect with the max sized URL)
            //read that much
            while (stream.Read(readBuffer, 0, 1) == 1)
            {
                if(readBuffer[0] == (byte)'\r')
                {
                    //spec requires a \n next
                    stream.Read(readBuffer, 0, 1);
                    if (readBuffer[0] != (byte)'\n')
                    {
                        throw new Exception("Malformed Gemini header - missing LF after CR");
                    }
                    break;
                }
                //keep going if we haven't read too many
                readCount++;
                if (readCount > ResponseLineMaxLen)
                {
                    throw new ApplicationException($"Invalid gemini response line. Did not find \\r\\n within {ResponseLineMaxLen} bytes");
                }
                respLineBuffer.Add(readBuffer[0]);
                CheckAbortTimeout();
            }

            //spec requires that the response line use UTF-8
            var respLine = Encoding.UTF8.GetString(respLineBuffer.ToArray());
            return new GemiResponse(url, respLine);
        }

        /// <summary>
        /// Reads the response body. Aborts if timeout or max size is exceeded
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>response body bytes</returns>
        private byte[] ReadBody(Stream stream)
        {
            var respBytes = new List<byte>(10 * 1024);
            var readBuffer = new byte[4096];


            int readCount = 0;
            do
            {
                readCount = stream.Read(readBuffer, 0, readBuffer.Length);
                if (readCount > 0)
                {
                    respBytes.AddRange(readBuffer.Take(readCount));
                }
                if (respBytes.Count > MaxResponseSize)
                {
                    throw new ApplicationException($"Requestor aborting due to reaching max download size {MaxResponseSize}.");
                }
                CheckAbortTimeout();
            }
            while (readCount > 0) ;

            DownloadTimer.Stop();
            return respBytes.ToArray();
        }

        private bool ShouldDownloadBody(GemiResponse resp)
        {
            if(!resp.IsSuccess)
            {
                return false;
            }
            if (OnlyDownloadText && !resp.MimeType.StartsWith("text/"))
            {
                resp.BodySkipped = true;
                return false;
            }
            return true;
        }

        private void CheckAbortTimeout()
        {
            if(AbortTimer.Elapsed.TotalMilliseconds > AbortTimeout)
            {
                throw new ApplicationException("Requestor abort timeout exceeded.");
            }
        }
    }
}
