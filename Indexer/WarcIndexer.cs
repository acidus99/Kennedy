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

        private readonly ResponseStore? _responseStore;
        private readonly ArchiveImporter? _archive;

        public WarcIndexer(ResponseStore? responseStore, ArchiveImporter? archive = null)
        {
            _responseStore = responseStore;
            _archive = archive;
        }

        public sealed record ImportSummary(long Records, long Responses, long Archived);

        public async Task<ImportSummary> IndexFileAsync(string warcPath, CancellationToken ct)
        {
            var batch = new List<GeminiResponse>(BatchSize);
            using var reader = new WarcReader(warcPath);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            double lastProgress = 0;
            long responses = 0, archived = 0;
            async Task Flush()
            {
                if (batch.Count == 0) return;
                if (_responseStore != null) await _responseStore.StoreBatchAsync(batch, ct);
                if (_archive != null) archived += _archive.StoreBatch(batch, ct);
                batch.Clear();
            }
            try
            {
                foreach (var record in reader)
                {
                    ct.ThrowIfCancellationRequested();
                    if (record is ResponseRecord responseRecord)
                    {
                        var response = GetGeminiResponse(responseRecord);
                        if (response != null)
                        {
                            responses++;
                            batch.Add(response);
                            if (batch.Count >= BatchSize) await Flush();
                        }
                    }
                    if (watch.Elapsed.TotalSeconds - lastProgress >= 5)
                    {
                        lastProgress = watch.Elapsed.TotalSeconds;
                        Console.WriteLine($"{Path.GetFileName(warcPath)}: {reader.RecordsRead:N0} records, {responses:N0} responses ({responses / Math.Max(1,lastProgress):N0} responses/s)");
                    }
                }
                await Flush();
            }
            catch (WarcFormatException ex)
            {
                await Flush();
                throw new InvalidDataException($"Partial import of {warcPath}; malformed WARC after {reader.RecordsRead:N0} records. The file has NOT been marked complete.", ex);
            }
            Console.WriteLine($"Imported {reader.RecordsRead:N0} records, {responses:N0} responses, {archived:N0} new archive captures in {watch.Elapsed.TotalSeconds:F1}s.");
            return new ImportSummary(reader.RecordsRead, responses, archived);
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
                throw new InvalidDataException("Certificate export is incomplete because the WARC is malformed.", ex);
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

            using var ownedCertificate = certificate;
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
