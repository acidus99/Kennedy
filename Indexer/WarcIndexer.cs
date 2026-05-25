using System;
using System.Collections.Generic;
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
    }
}
