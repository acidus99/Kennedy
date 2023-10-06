using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Kennedy.WarcConverters.MozzPortalImport;

public class HttpRequestor
{
    HttpClient Client;
    public HttpResponseMessage? Response;

    public HttpRequestor()
    {
        Client = new HttpClient(new HttpClientHandler
        {
            CheckCertificateRevocationList = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        });

        Client.Timeout = TimeSpan.FromSeconds(60);
        Client.DefaultRequestHeaders.UserAgent.TryParseAdd("GeminiProxy/0.1 (gemini://gemi.dev/) gemini-proxy/0.1");
    }

    public string ErrorMessage { get; internal set; } = "";

    public HttpResponseMessage SendRequest(Uri url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        return Client.Send(request, HttpCompletionOption.ResponseContentRead);
    }

    public static byte[] ReadFully(Stream input)
    {
        byte[] buffer = new byte[16 * 1024];
        using (MemoryStream ms = new MemoryStream())
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
    }

}
