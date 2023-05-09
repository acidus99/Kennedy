using System;
using System.Threading.Tasks;

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
            var outputDirectory = "";

            foreach(var inputWarc in File.ReadAllLines("foo"))
            {
                //SearchProcessor processor = new SearchProcessor(outputDirectory, "config/");
                IWarcProcessor processor = new ArchiveProcessor(outputDirectory, "config/");

                WarcWrapper warcWrapper = new WarcWrapper(new WarcParser(inputWarc));

                WarcRecord? record = null;
                while ((record = warcWrapper.GetNext()) != null)
                {
                    processor.ProcessRecord(record);
                }
                processor.FinalizeProcessing();
            }
        }

    }

}