using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Gemini.Net;
using Kennedy.Data.Services;
using WarcDotNet;

namespace Kennedy.Indexer
{
    /// <summary>
    /// Reads a WARC file and indexes its Gemini response records into the Kennedy database
    /// via <see cref="ResponseStore.StoreBatchAsync"/>.
    ///
    /// Records are accumulated in memory up to <see cref="BatchSize"/> then flushed as a
    /// single SQLite transaction, reducing per-record commit overhead from O(N) to O(N/BatchSize).
    /// </summary>
    public sealed class WarcIndexer
    {
        public sealed class CertificateSummary
        {
            public required string Host { get; init; }
            public required int Port { get; init; }
            public required string PublicKeySha256 { get; set; }
            public required DateTime LastSeenUtc { get; set; }
            public required int TimesSeen { get; set; }
            public DateTime NotAfterUtc { get; set; }
        }

        /// <summary>
        /// Number of responses written per SQLite transaction.
        /// 500 stays well under SQLite's default 999-parameter IN-clause limit while
        /// providing a large reduction in commit overhead.
        /// </summary>
        public const int BatchSize = 500;

        private readonly ResponseStore _responseStore;

        public WarcIndexer(ResponseStore responseStore)
        {
            _responseStore = responseStore;
        }

        /// <summary>
        /// Iterates every WARC record in <paramref name="warcPath"/>, accumulates Gemini
        /// response records into batches, and flushes each batch via <see cref="ResponseStore.StoreBatchAsync"/>.
        /// Non-Gemini records and records without a target URI or body are silently skipped.
        /// A malformed WARC record logs a warning and halts processing of that file; any
        /// accumulated partial batch is still flushed before returning.
        /// </summary>
        public async Task IndexFileAsync(string warcPath, CancellationToken ct)
        {
            var batch = new List<GeminiResponse>(BatchSize);

            using var reader = new WarcReader(warcPath);
            var start = DateTime.Now;

            try
            {
                foreach (WarcRecord record in reader)
                {
                    if (reader.RecordsRead % 100 == 0)
                    {
                        var elapsedSeconds = Math.Max(1, Math.Truncate(DateTime.Now.Subtract(start).TotalSeconds));
                        var ratePerSecond = Math.Truncate(reader.RecordsRead / elapsedSeconds);
                        Console.Write(
                            $"{reader.Filename}\t{reader.RecordsRead}\t {elapsedSeconds} s ({ratePerSecond} / s)    \r");
                    }

                    if (record is not ResponseRecord respRecord)
                    {
                        continue;
                    }

                    var response = GetGeminiResponse(respRecord);
                    if (response == null)
                    {
                        continue;
                    }

                    batch.Add(response);

                    if (batch.Count >= BatchSize)
                    {
                        await _responseStore.StoreBatchAsync(batch, ct);
                        batch.Clear();
                    }
                }
            }
            catch (WarcFormatException ex)
            {
                Console.Error.WriteLine("\nMalformed WARC!");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("Assuming the rest of the WARC is bad and skipping the rest!");
            }

            // Flush any remaining records that didn't fill a complete batch.
            if (batch.Count > 0)
            {
                await _responseStore.StoreBatchAsync(batch, ct);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Like <see cref="IndexFileAsync"/> but only updates UrlRegistry — skips Documents, Images, FTS, and Links.
        /// Used for Phase-1 bootstrap: call this on every WARC (any order) so UrlRegistry reflects
        /// the most recent known status for each URL before the full-index pass runs.
        /// </summary>
        public async Task IndexFileRegistryOnlyAsync(string warcPath, CancellationToken ct)
        {
            var batch = new List<GeminiResponse>(BatchSize);

            using var reader = new WarcReader(warcPath);
            var start = DateTime.Now;

            try
            {
                foreach (WarcRecord record in reader)
                {
                    if (reader.RecordsRead % 100 == 0)
                    {
                        var elapsedSeconds = Math.Max(1, Math.Truncate(DateTime.Now.Subtract(start).TotalSeconds));
                        var ratePerSecond = Math.Truncate(reader.RecordsRead / elapsedSeconds);
                        Console.Write(
                            $"{reader.Filename}\t{reader.RecordsRead}\t {elapsedSeconds} s ({ratePerSecond} / s)    \r");
                    }

                    if (record is not ResponseRecord respRecord) continue;

                    var response = GetGeminiResponse(respRecord);
                    if (response == null) continue;

                    batch.Add(response);

                    if (batch.Count >= BatchSize)
                    {
                        await _responseStore.StoreRegistryOnlyBatchAsync(batch, ct);
                        batch.Clear();
                    }
                }
            }
            catch (WarcFormatException ex)
            {
                Console.Error.WriteLine("\nMalformed WARC!");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("Assuming the rest of the WARC is bad and skipping the rest!");
            }

            if (batch.Count > 0)
            {
                await _responseStore.StoreRegistryOnlyBatchAsync(batch, ct);
            }

            Console.WriteLine();
        }

        public async Task WriteCertificateListCsvAsync(string warcPath, string outputCsvPath, CancellationToken ct)
        {
            var summaries = GetCertificateSummaries(warcPath, ct);

            await using var writer = new StreamWriter(outputCsvPath);
            await writer.WriteLineAsync("host,port,public_key_sha256,last_seen_utc,times_seen,not_after_utc");

            foreach (var summary in summaries.Values)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{EscapeCsv(summary.Host)},{summary.Port},{summary.PublicKeySha256},{summary.LastSeenUtc:O},{summary.TimesSeen},{summary.NotAfterUtc:O}"));
            }
        }

        /// <summary>
        /// Converts a <see cref="ResponseRecord"/> from the WARC file into a <see cref="GeminiResponse"/>.
        /// Returns null when the record target URI is missing, lacks a body, or is not a gemini:// URL.
        /// Sets <c>RequestSent</c>/<c>ResponseReceived</c> from the WARC date and preserves the truncation flag.
        /// </summary>
        private static GeminiResponse? GetGeminiResponse(ResponseRecord responseRecord)
        {
            if (responseRecord.TargetUri == null || responseRecord.ContentBlock == null
                || responseRecord.TargetUri.Scheme != "gemini")
            {
                return null;
            }

            var url = new GeminiUrl(responseRecord.TargetUri);
            var response = GeminiParser.ParseResponseBytes(url, responseRecord.ContentBlock);
            response.RequestSent = responseRecord.Date;
            response.ResponseReceived = responseRecord.Date;
            response.IsBodyTruncated = responseRecord.Truncated?.Length > 0;
            return response;
        }

        private static Dictionary<string, CertificateSummary> GetCertificateSummaries(string warcPath, CancellationToken ct)
        {
            var summaries = new Dictionary<string, CertificateSummary>(StringComparer.OrdinalIgnoreCase);

            using var reader = new WarcReader(warcPath);
            var start = DateTime.Now;

            try
            {
                foreach (WarcRecord record in reader)
                {
                    ct.ThrowIfCancellationRequested();

                    if (reader.RecordsRead % 100 == 0)
                    {
                        var elapsedSeconds = Math.Max(1, Math.Truncate(DateTime.Now.Subtract(start).TotalSeconds));
                        var ratePerSecond = Math.Truncate(reader.RecordsRead / elapsedSeconds);
                        Console.Write(
                            $"{reader.Filename}\t{reader.RecordsRead}\t {elapsedSeconds} s ({ratePerSecond} / s)    \r");
                    }

                    if (record is not MetadataRecord metadataRecord)
                    {
                        continue;
                    }

                    var summary = GetCertificateSummary(metadataRecord);
                    if (summary == null)
                    {
                        continue;
                    }

                    var key = $"{summary.Host}:{summary.Port}";
                    if (!summaries.TryGetValue(key, out var existing))
                    {
                        summaries.Add(key, summary);
                        continue;
                    }

                    existing.TimesSeen += summary.TimesSeen;
                    if (summary.LastSeenUtc > existing.LastSeenUtc)
                    {
                        existing.PublicKeySha256 = summary.PublicKeySha256;
                        existing.LastSeenUtc = summary.LastSeenUtc;
                        existing.NotAfterUtc = summary.NotAfterUtc;
                    }
                }
            }
            catch (WarcFormatException ex)
            {
                Console.Error.WriteLine("\nMalformed WARC!");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("Assuming the rest of the WARC is bad and skipping the rest!");
            }

            Console.WriteLine();
            return summaries;
        }

        private static CertificateSummary? GetCertificateSummary(MetadataRecord metadataRecord)
        {
            if (metadataRecord.TargetUri == null
                || metadataRecord.TargetUri.Scheme != "gemini"
                || string.IsNullOrWhiteSpace(metadataRecord.ContentText)
                || !string.Equals(metadataRecord.ContentType, "application/x-pem-file", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            X509Certificate2 certificate;
            try
            {
                certificate = X509Certificate2.CreateFromPem(metadataRecord.ContentText);
            }
            catch (CryptographicException)
            {
                return null;
            }

            var publicKeyHash = SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo());
            return new CertificateSummary
            {
                Host = metadataRecord.TargetUri.Host,
                Port = metadataRecord.TargetUri.Port,
                PublicKeySha256 = Convert.ToHexString(publicKeyHash).ToLowerInvariant(),
                LastSeenUtc = metadataRecord.Date.ToUniversalTime(),
                TimesSeen = 1,
                NotAfterUtc = certificate.NotAfter.ToUniversalTime(),
            };
        }

        private static string EscapeCsv(string value)
        {
            if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
