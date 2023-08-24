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
        static void Main(string[] args)
        {
            var outputDirectory = ResolveDir("~/tmp/");

            //IWarcProcessor processor = new SearchProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));
            //IWarcProcessor processor = new ArchiveProcessor(outputDirectory);
            //string inputWarc = ResolveDir("~/HDD Inside/Kennedy-Work/WARCs/2023-05-24.warc");

            //ProcessWarc(inputWarc, processor);

            foreach (var inputWarc in File.ReadAllLines(ResolveDir("~/HDD Inside/Kennedy-Work/WARCs/all.txt")))
            {
                //IWarcProcessor processor = new SearchProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));
                IWarcProcessor processor = new ArchiveProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));
                ProcessWarc(inputWarc, processor);
            }
        }

        static void ProcessWarc(string inputWarc, IWarcProcessor processor)
        {
            WarcWrapper warcWrapper = new WarcWrapper(new WarcParser(inputWarc));

            WarcRecord? record = null;
            while ((record = warcWrapper.GetNext()) != null)
            {
                processor.ProcessRecord(record);
            }
            processor.FinalizeProcessing();
        }

        private static string ResolveDir(string dir)
            => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
    }
}