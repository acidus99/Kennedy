using System;
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
        private readonly ResponseStore _responseStore;

        public WarcIndexer(ResponseStore responseStore)
        {
            _responseStore = responseStore;
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

                            await _responseStore.StoreResponseAsync(response, ct);
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

    }
}
