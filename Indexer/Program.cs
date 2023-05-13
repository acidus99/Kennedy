using System;
using System.Threading.Tasks;

using Gemini.Net;
using Kennedy.Data.Parsers;

using Kennedy.SearchIndex.Search;


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
            var outputDirectory = ResolveDir("~/HDD Inside/Kennedy-Work/output/");

            foreach (var inputWarc in File.ReadAllLines(ResolveDir("~/HDD Inside/Kennedy-Work/WARCs/latest.txt")))
            {
                IWarcProcessor processor = new SearchProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));
                //IWarcProcessor processor = new ArchiveProcessor(outputDirectory);

                WarcWrapper warcWrapper = new WarcWrapper(new WarcParser(inputWarc));

                WarcRecord? record = null;
                while ((record = warcWrapper.GetNext()) != null)
                {
                    processor.ProcessRecord(record);
                }
                processor.FinalizeProcessing();
            }
        }

        private static string ResolveDir(string dir)
            => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
    }
}