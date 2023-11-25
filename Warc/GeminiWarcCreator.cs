using System;
using System.Collections.Specialized;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Gemini.Net;
using Kennedy.Data;

using WarcDotNet;

namespace Kennedy.Warc
{
    public class GeminiWarcCreator : WarcWriter
    {
        public const string RequestContentType = "application/gemini; msgtype=request";
        public const string ResponseContentType = "application/gemini; msgtype=response";

        public Uri WarcInfoID { get; private set; }

        /// <summary>
        /// Tracks what authorities we have written metadata records about their certificates
        /// </summary>
        private Dictionary<string, bool> WrittenCertificates;

        public GeminiWarcCreator(string outputFile)
            : base(outputFile)
        {
            WarcInfoID = WarcRecord.CreateId();
            WrittenCertificates = new Dictionary<string, bool>();
        }

        public void WriteWarcInfo(WarcFields fields)
        {
            Write(new WarcInfoRecord
            {
                Id = WarcInfoID,
                ContentType = WarcFields.ContentType,
                ContentText = fields.ToString()
            });
        }

        public void WriteSession(GeminiResponse response, TlsConnectionInfo? connectionInfo = null)
        {
            var requestRecord = CreateRequestRecord(response.RequestUrl);
            requestRecord.SetDate(response.RequestSent);

            requestRecord.IpAddress = response.RemoteAddress?.ToString();
            AppendTlsInfo(requestRecord, connectionInfo);

            Write(requestRecord);

            var responseRecord = new ResponseRecord
            {
                ContentBlock = GeminiParser.CreateResponseBytes(response),
                ContentType = ResponseContentType,
                WarcInfoId = WarcInfoID,
                TargetUri = response.RequestUrl.Url,
                PayloadDigest = GetPayloadDigest(response),
                IpAddress = response.RemoteAddress?.ToString(),
                IdentifiedPayloadType = response.MimeType
            };

            responseRecord.SetDate(response.ResponseReceived);
            SetBlockDigest(responseRecord);
            AppendTlsInfo(responseRecord, connectionInfo);

            responseRecord.ConcurrentTo.Add(requestRecord.Id);

            if (response.IsBodyTruncated)
            {
                responseRecord.Truncated = "length";
            }

            Write(responseRecord);

            if(connectionInfo != null && connectionInfo.RemoteCertificate != null && ShouldCreateCertificateRecord(response.RequestUrl))
            {
                var metadataRecord = new MetadataRecord
                {
                    WarcInfoId = WarcInfoID,
                    ReferersTo = responseRecord.Id,
                    ContentText = connectionInfo.RemoteCertificate.ExportCertificatePem(),
                    ContentType = "application/x-pem-file",
                    TargetUri = response.RequestUrl.Url
                };
                metadataRecord.SetDate(response.ResponseReceived);
                SetBlockDigest(metadataRecord);

                Write(metadataRecord);
            }
        }

        public void WriteLegacySession(GeminiUrl url, DateTime sent, int statusCode, string meta, string mime, byte[]? bytes, bool isTruncated = false)
        {

            var requestRecord = CreateRequestRecord(url);
            requestRecord.Date = sent;

            Write(requestRecord);

            var responseRecord = new ResponseRecord
            {
                ContentBlock = GeminiParser.CreateResponseBytes(statusCode, meta, bytes),
                ContentType = ResponseContentType,
                Date = sent,
                WarcInfoId = WarcInfoID,
                TargetUri = url.Url
            };
            SetBlockDigest(responseRecord);
            responseRecord.ConcurrentTo.Add(requestRecord.Id);

            //do we have a mime?
            if (!string.IsNullOrEmpty(mime))
            {
                responseRecord.IdentifiedPayloadType = mime;
            }
            //was it truncated?
            if (isTruncated)
            {
                responseRecord.Truncated = "length";
            }

            if(bytes != null)
            {
                responseRecord.PayloadDigest = GetPayloadDigest(bytes);
            }

            Write(responseRecord);
        }

        public void WriteLegacyCertificate(DateTime captured, GeminiUrl url, X509Certificate2 certificate)
        {
            var metadataRecord = new MetadataRecord
            {
                WarcInfoId = WarcInfoID,
                ContentText = certificate.ExportCertificatePem(),
                ContentType = "application/x-pem-file",
                TargetUri = url.Url,
                Date = captured,
            };
            SetBlockDigest(metadataRecord);
            Write(metadataRecord);
        }

        private bool ShouldCreateCertificateRecord(GeminiUrl url)
        {
            if(!WrittenCertificates.ContainsKey(url.Authority))
            {
                WrittenCertificates.Add(url.Authority, true);
                return true;
            }
            return false;
        }

        private RequestRecord CreateRequestRecord(GeminiUrl url)
        {
            var record = new RequestRecord
            {
                ContentBlock = GeminiParser.CreateRequestBytes(url),
                ContentType = RequestContentType,
                WarcInfoId = WarcInfoID,
                TargetUri = url.Url
            };

            SetBlockDigest(record);
            return record;
        }

        private void SetBlockDigest(WarcRecord record)
        {
            if(record.ContentBlock != null)
            {
                record.BlockDigest = GeminiParser.GetStrongHash(record.ContentBlock);
            }
        }

        private void AppendTlsInfo(WarcRecord record, TlsConnectionInfo? connectionInfo)
        {
            if (connectionInfo != null)
            {
                if (connectionInfo.Protocol.HasValue)
                {
                    record.AddCustomHeader("WARC-Protocol", GetProtocolHeader(connectionInfo));
                }

                if (connectionInfo.CipherSuite.HasValue)
                {
                    record.AddCustomHeader("WARC-TLS-Cipher-Suite", GetCipherSuite(connectionInfo));
                }
            }
        }

        private string? GetCipherSuite(TlsConnectionInfo connection)
        {
            if (connection.CipherSuite != null)
            {
                return connection.CipherSuite.ToString();
            }
            return null;
        }

        private string? GetPayloadDigest(GeminiResponse response)
            => GetPayloadDigest(response.BodyBytes);

        private string? GetPayloadDigest(byte[]? bodyBytes)
           => bodyBytes != null ?
               GeminiParser.GetStrongHash(bodyBytes) :
               null;

        private string? GetProtocolHeader(TlsConnectionInfo connection)
        {
            if(connection.Protocol == null)
            {
                return null;
            }

            // Disabling warning because we are only using these to parse
#pragma warning disable SYSLIB0039 // Type or member is obsolete
            switch (connection.Protocol)
            {
                case SslProtocols.Tls13:
                    return "tls/1.3";
                case SslProtocols.Tls12:
                    return "tls/1.2";
                case SslProtocols.Tls11:
                    return "tls/1.1";
                case SslProtocols.Tls:
                    return "tls/1.0";
                default:
                    return null;
            }
#pragma warning restore SYSLIB0039 // Type or member is obsolete
        }

    }
}
