using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Gemini.Net;
using Kennedy.Data.Services;
using WarcDotNet;

namespace Kennedy.Indexer
{
    /// <summary>
    /// Minimal “indexer” skeleton. Replace the record enumeration with a real WARC parser.
    /// </summary>
    public sealed class WarcIndexer
    {
        private readonly UrlRegistryStore _store;

        public WarcIndexer(UrlRegistryStore store)
        {
            _store = store;
        }

        public async Task IndexFileAsync(string warcPath, CancellationToken ct)
        {
            using (WarcReader reader = new WarcReader(warcPath))
            {
                DateTime start = DateTime.Now;
                DateTime prev = start;

                try
                {

                    foreach (WarcRecord record in reader)
                    {

                        if (reader.RecordsRead % 100 == 0)
                        {
                            var elapsedSeconds = Math.Truncate(DateTime.Now.Subtract(start).TotalSeconds);
                            var ratePerSecond = Math.Truncate(reader.RecordsRead / elapsedSeconds);
                            Console.Write(
                                $"{reader.Filename}\t{reader.RecordsRead}\t {elapsedSeconds} s ({ratePerSecond} / s)    ");
                            Console.Write('\r');
                            prev = DateTime.Now;
                        }

                        if (record is ResponseRecord respRecord)
                        {
                            GeminiResponse? response = GetGeminiResponse(respRecord);
                            if (response == null)
                            {
                                continue;
                            }

                            await _store.AddOrUpdateAsync(
                                normalizedUrl: response.RequestUrl.NormalizedUrl,
                                lastStatusCode: response.StatusCode,
                                contentHash: response.Hash,
                                visitTimeUtc: response.RequestSent ?? DateTime.MinValue,
                                meta: response.Meta,
                                ct: ct);
                        }
                    }
                }
                catch (WarcFormatException ex)
                {
                    Console.Error.WriteLine("Malformed WARC!");
                    Console.Error.WriteLine(ex.Message);
                    Console.Error.WriteLine("Assuming the rest of the WARC is bad and skipping the rest!");
                }

                Console.WriteLine();
            }
        }

        private GeminiResponse? GetGeminiResponse(ResponseRecord responseRecord)
        {
            if (responseRecord.TargetUri == null || responseRecord.ContentBlock == null || responseRecord.TargetUri.Scheme != "gemini")
            {
                return null;
            }

            var url = new GeminiUrl(responseRecord.TargetUri);
            var response = GeminiParser.ParseResponseBytes(url, responseRecord.ContentBlock);
            response.RequestSent = responseRecord.Date;
            response.ResponseReceived = responseRecord.Date;
            response.IsBodyTruncated = (responseRecord.Truncated?.Length > 0);
            return response;
        }

        private static string? NormalizeUrl(string raw)
        {
            raw = raw.Trim();
            if (raw.Length == 0)
                return null;

            // Placeholder normalization.
            // Swap this out for your real GeminiUrl / normalization logic.
            return raw;
        }

        private static async IAsyncEnumerable<string> ReadUrlsAsLinesAsync(
            string path,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line != null)
                    yield return line;
            }
        }
    }
}
