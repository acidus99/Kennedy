using System;

using Gemini.Net;
using Kennedy.Data.Parsers;

using Warc;

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

            var outputDirectory = "/Users/billy/tmp/";
            var inputWarc = "/Users/billy/HDD Inside/Kennedy-Work/WARCs/2022-03-01.warc";

            SearchProcessor processor = new SearchProcessor(outputDirectory, "config/");

            WarcParser warcParser = new WarcParser(inputWarc);
            int count = 0;
            while(warcParser.HasRecords)
            {
                var record = warcParser.GetNext();
                processor.ProcessRecord(record!);

                count++;
                if (count % 100 == 0) Console.WriteLine($"Ingesting\t{count} {record.Type} {record.Id}");
            }
            processor.FinalizeProcessing();

            Console.WriteLine("loop complete");



        }

    }

}