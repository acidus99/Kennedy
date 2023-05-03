using System;

using Gemini.Net;
using Kennedy.Data.Parsers;

using Toimik.WarcProtocol;

using Kennedy.Indexer.WarcProcessors;

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

            SearchProcessor processor = new SearchProcessor("/Users/billy/tmp/", "config/");

            WarcParser warcParser = new WarcParser();
            int count = 0;
            await foreach (Record record in warcParser.Parse("/Users/billy/HDD Inside/Kennedy-Work/WARCs/2022-03-01.warc"))
            {
                processor.ProcessRecord(record);

                count++;
                if(count % 100 == 0) Console.WriteLine($"Ingesting\t{count} {record.Type} {record.Id}");

            }
            processor.FinalizeProcessing();

            Console.WriteLine("loop complete");



        }

    }

}