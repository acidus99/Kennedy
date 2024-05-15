using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Gemini.Net;

namespace Kennedy.Server.Helpers;

public class CertChecker
{
    X509Certificate2? cert;

    public bool Request(GeminiUrl url)
    {
        GeminiRequestor requestor = new GeminiRequestor { AbortTimeout = 10000, ConnectionTimeout = 10000 };
        var resp = requestor.Request(url);

        cert = resp.TlsInfo?.RemoteCertificate;

        return (cert != null);
    }

    private byte[] GetCertificateBytes()
        => cert!.GetRawCertData();

    private byte[] GetPublicKeyBytes()
        => cert!.PublicKey.ExportSubjectPublicKeyInfo();

    public string GenericCertificateSha256()
    {
        if (cert == null)
            return "";

        return FormatHash(SHA256.HashData(GetCertificateBytes()), false, false);
    }

    public string GenericCertificateMd5()
    {
        if (cert == null)
            return "";

        return FormatHash(MD5.HashData(GetCertificateBytes()), false, false);
    }

    public string GenericPublicKeySha256()
    {
        if (cert == null)
            return "";

        return FormatHash(SHA256.HashData(GetPublicKeyBytes()), false, false);
    }

    public string ClientElaho()
    {
        if (cert == null)
            return "";

        return FormatHash(MD5.HashData(GetCertificateBytes()));
    }


    public string ClientKristall()
    {
        if (cert == null)
            return "";

        return FormatHash(SHA256.HashData(GetCertificateBytes()));
    }

    public string ClientLagrange()
    {
        if (cert == null)
            return "";

        return FormatHash(SHA256.HashData(GetPublicKeyBytes()), false, false);
    }

    private string FormatHash(byte[] data, bool makeUpper = true, bool addSeparator = true)
    {
        string hash = Convert.ToHexString(data);
        hash = (makeUpper) ? hash.ToUpper() : hash.ToLower();

        if (addSeparator)
        {
            hash = AddSeparators(hash, ':');
        }
        return hash;
    }

    private string AddSeparators(string hash, char separator)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < hash.Length; i += 2)
        {
            sb.Append(hash[i]);
            sb.Append(hash[i + 1]);
            if (i + 2 < hash.Length)
            {
                sb.Append(separator);
            }
        }
        return sb.ToString();
    }
}