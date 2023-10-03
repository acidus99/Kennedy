using System;
using System.Threading.Tasks;

using Gemini.Net;
using Kennedy.Data.Parsers;

using Kennedy.SearchIndex.Search;

using Kennedy.Warc;

using WarcDotNet;

using Kennedy.Indexer.WarcProcessors;
using System.Diagnostics;

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

            FixWarc("/Users/billy/HDD Inside/Kennedy-Work/WARCs/2023-04-27.warc");
            FixWarc("/Users/billy/HDD Inside/Kennedy-Work/WARCs/2023-05-24.warc");
            FixWarc("/Users/billy/HDD Inside/Kennedy-Work/WARCs/2023-05-30.warc");
            return;

            //var outputDirectory = ResolveDir("~/kennedy-capsule/crawl-data/");
            var outputDirectory = ResolveDir("~/tmp/");

            IWarcProcessor processor = new SearchProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));
            //IWarcProcessor processor = new ArchiveProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));

            //string inputWarc = ResolveDir("~/HDD Inside/Kennedy-Work/WARCs/2023-09-08.warc");
            string inputWarc = ResolveDir("~/HDD Inside/Kennedy-Work/gzipped/2023-09-08.warc.gz");

            if(args.Length > 0)
            {
                inputWarc = args[0];
            }


            ProcessWarc(inputWarc, processor);


            //foreach (var inputWarc in File.ReadAllLines(ResolveDir("~/HDD Inside/Kennedy-Work/WARCs/all.txt")))
            //foreach (var inputWarc in Directory.GetFiles(ResolveDir("~/HDD Inside/Kennedy-Work/WARCs/"), "*.warc"))
            //{
            //    //IWarcProcessor processor = new SearchProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));
            //    IWarcProcessor processor = new ArchiveProcessor(outputDirectory, ResolveDir("~/kennedy-capsule/config/"));
            //    ProcessWarc(inputWarc, processor);
            //}
        }

        /*
         * 	Processed++;
        */

        static void FixWarc(string inputWarc)
        {
            string filename = Path.GetFileName(inputWarc);

            string outputWarc = ResolveDir("~/tmp/") + filename;

            WarcTruncater.Fix(inputWarc, outputWarc);
        }

        static void ProcessWarc(string inputWarc, IWarcProcessor processor)
        {
            using (WarcReader reader = new WarcReader(inputWarc))
            {
                DateTime prev = DateTime.Now;
                foreach(WarcRecord record in reader)
                {
                    processor.ProcessRecord(record);
                    if (reader.RecordsRead % 100 == 0)
                    {
                        Console.WriteLine($"{reader.Filename}\t{reader.RecordsRead}\t{Math.Truncate(DateTime.Now.Subtract(prev).TotalMilliseconds)} ms");
                        prev = DateTime.Now;
                    }
                }
                processor.FinalizeProcessing();
            }
        }

        private static string ResolveDir(string dir)
            => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
    }
}