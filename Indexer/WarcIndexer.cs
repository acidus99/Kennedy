using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Diagnostics;
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
            // One reader overlaps decompression with two independent database writers.
            // Bodies and order are preserved; parsing/classification itself is unchanged.
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token=cancellation.Token;
            var channel=Channel.CreateBounded<ImportBatch>(new BoundedChannelOptions(2)
            {
                SingleReader=true, SingleWriter=true, FullMode=BoundedChannelFullMode.Wait
            });
            using var reader=new WarcResponseReader(warcPath);
            var watch=Stopwatch.StartNew();
            long responses=0,archived=0,records=0;
            double searchSeconds=0,archiveSeconds=0,lastProgress=0;
            long parsedBefore=_responseStore?.ParsedResponses ?? 0;
            long unchangedBefore=_responseStore?.UnchangedResponses ?? 0;
            long archiveSeenBefore=_archive?.AlreadyArchived ?? 0;
            var producer=Task.Run(async () =>
            {
                var batch=new List<GeminiResponse>(BatchSize);
                long bytes=0,count=0;
                try
                {
                    foreach (var record in reader.ReadResponses(token))
                    {
                        var response=GetGeminiResponse(record);
                        if (response==null) continue;
                        // Hash once before either consumer touches the shared response.
                        if (_responseStore!=null) _=response.Hash;
                        count++; bytes+=response.BodySize; batch.Add(response);
                        if (batch.Count>=BatchSize || bytes>=32L*1024*1024)
                        {
                            await channel.Writer.WriteAsync(new(batch,reader.RecordsRead,count,reader.CompressedBytesRead),token);
                            batch=new(BatchSize); bytes=0;
                        }
                    }
                    if (batch.Count>0) await channel.Writer.WriteAsync(new(batch,reader.RecordsRead,count,reader.CompressedBytesRead),token);
                    channel.Writer.TryComplete();
                }
                catch (Exception ex)
                {
                    // Commit the valid prefix even when a later record is malformed.
                    if (ex is WarcFormatException && batch.Count>0)
                    {
                        try { await channel.Writer.WriteAsync(new(batch,reader.RecordsRead,count,reader.CompressedBytesRead),token); }
                        catch (OperationCanceledException) { }
                    }
                    channel.Writer.TryComplete(ex);
                }
            },token);
            try
            {
                await foreach (var batch in channel.Reader.ReadAllAsync(token))
                {
                    var archiveTask=_archive==null ? Task.CompletedTask : Task.Run(() =>
                    {
                        var stage=Stopwatch.StartNew();
                        archived+=_archive.StoreBatch(batch.Responses,token);
                        archiveSeconds+=stage.Elapsed.TotalSeconds;
                    },token);
                    try
                    {
                        if (_responseStore!=null)
                        {
                            var stage=Stopwatch.StartNew();
                            await _responseStore.StoreBatchAsync(batch.Responses,token);
                            searchSeconds+=stage.Elapsed.TotalSeconds;
                        }
                    }
                    finally { await archiveTask; }
                    responses=batch.Count; records=batch.Records;
                    if (watch.Elapsed.TotalSeconds-lastProgress>=5)
                    {
                        lastProgress=watch.Elapsed.TotalSeconds;
                        Console.WriteLine($"{Path.GetFileName(warcPath)}: {records:N0} records, {responses:N0} responses ({responses / Math.Max(1,lastProgress):N0} responses/s), {100.0*batch.Bytes/Math.Max(1,reader.FileLength):F1}% of WARC bytes read; skipped {_responseStore?.UnchangedResponses-unchangedBefore ?? 0:N0} unchanged search, {_archive?.AlreadyArchived-archiveSeenBefore ?? 0:N0} existing captures");
                    }
                }
                await producer;
            }
            catch (WarcFormatException ex)
            {
                throw new InvalidDataException($"Partial import of {warcPath}; malformed WARC after {reader.RecordsRead:N0} records. The file has NOT been marked complete.",ex);
            }
            finally
            {
                cancellation.Cancel();
                try { await producer; } catch (OperationCanceledException) { }
            }
            records=reader.RecordsRead;
            Console.WriteLine($"Imported {records:N0} records, {responses:N0} responses, {archived:N0} new archive captures in {watch.Elapsed.TotalSeconds:F1}s.");
            Console.WriteLine($"Search: {_responseStore?.ParsedResponses-parsedBefore ?? 0:N0} parsed, {_responseStore?.UnchangedResponses-unchangedBefore ?? 0:N0} unchanged skipped; {searchSeconds:F1}s. Archive: {_archive?.AlreadyArchived-archiveSeenBefore ?? 0:N0} existing captures skipped; {archiveSeconds:F1}s. Stages overlap.");
            return new ImportSummary(records,responses,archived);
        }

        private sealed record ImportBatch(List<GeminiResponse> Responses,long Records,long Count,long Bytes);

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
