using System;
using System.Collections.Specialized;
using System.Security.Authentication;
using System.Text;

using Gemini.Net;
using Kennedy.Data;

using Warc;

namespace Kennedy.Warc
{
    public class GeminiWarcCreator : WarcWriter
    {
        public const string RequestContentType = "application/gemini; msgtype=request";
        public const string ResponseContentType = "application/gemini; msgtype=response";

        public Uri WarcInfoID { get; private set; }

        public GeminiWarcCreator(string outputFile)
            : base(outputFile)
        {
            WarcInfoID = WarcRecord.CreateId();
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

        public void WriteSession(GeminiResponse response, TlsConnectionInfo? connectionInfo)
        {
            var requestRecord = CreateRequestRecord(response.RequestUrl);
            if (response.RequestSent.HasValue)
            {
                requestRecord.Date = response.RequestSent.Value;
            }
            requestRecord.IpAddress = response.RemoteAddress?.ToString();
            AppendTlsInfo(requestRecord, connectionInfo);

            Write(requestRecord);

            var responseRecord = new ResponseRecord
            {
                ContentBlock = GeminiParser.CreateResponseBytes(response),
                ContentType = ResponseContentType,
                WarcInfoId = WarcInfoID,
                TargetUri = response.RequestUrl.Url
            };

            responseRecord.BlockDigest = GeminiParser.GetStrongHash(responseRecord.ContentBlock);

            if (response.ResponseReceived.HasValue)
            {
                responseRecord.Date = response.ResponseReceived.Value;
            }
            responseRecord.IpAddress = response.RemoteAddress?.ToString();
            AppendTlsInfo(requestRecord, connectionInfo);

            responseRecord.ConcurrentTo.Add(requestRecord.Id);

            if (response.MimeType != null)
            {
                responseRecord.IdentifiedPayloadType = response.MimeType;
            }

            if (response.IsBodyTruncated)
            {
                responseRecord.Truncated = "length";
            }

            Write(responseRecord);
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

            Write(responseRecord);
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

            record.BlockDigest = GeminiParser.GetStrongHash(record.ContentBlock);
            return record;
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
