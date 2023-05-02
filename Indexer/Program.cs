using System;

using Gemini.Net;
using Kennedy.Data.Parsers;

using Toimik.WarcProtocol;

namespace Kennedy.Indexer
{
    class Program
    {
        /// <summary>
        /// Consumes a WARC and generates a search index
        /// </summary>
        /// <param name="args"></param>
        static async Task Main(string[] args)
        {
            RecordIngester ingester = new RecordIngester("", "config/");

            WarcParser warcParser = new WarcParser();
            int count = 0;
            await foreach (Record record in warcParser.Parse(""))
            {
                count++;
                Console.WriteLine($"Ingesting\t{count} {record.Type} {record.Id}");
                ingester.Ingest(record);
            }
            Console.WriteLine("loop complete");
            ingester.CompleteImport();



        }

    }

}