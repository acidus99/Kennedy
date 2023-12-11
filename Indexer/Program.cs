using Kennedy.Indexer.WarcProcessors;

namespace Kennedy.Indexer;

class Program
{
    /// <summary>
    /// Consumes a WARC and generates a search index
    /// </summary>
    /// <param name="args"></param>
    static void Main(string[] args)
    {

        Console.WriteLine("Kennedy Indexer");
        if (args.Length != 2)
        {
            Console.WriteLine("Incorrect number of parameters.");
            Console.WriteLine("Usage: Indexer [Output Directory] [Input WARC file]");
            return;
        }

        string outputDir = args[0];
        string inputWarc = args[1];

        if(!Directory.Exists(outputDir))
        {
            Console.WriteLine($"Cannot read output directory '{outputDir}'");
            return;
        }

        if(!File.Exists(inputWarc))
        {
            Console.WriteLine($"Cannot read input WARC file '{inputWarc}'");
            return;
        }

        string configDir = ResolveDir("~/kennedy-capsule/config/");

        IWarcProcessor searchProcessor = new SearchProcessor(outputDir, configDir);
        IWarcProcessor archiveProcessor = new ArchiveProcessor(outputDir, configDir);

        ProcessWarc(inputWarc, searchProcessor, archiveProcessor);
    }

    static void ProcessWarc(string inputWarc, params IWarcProcessor [] processors)
    {
        using (WarcReader reader = new WarcReader(inputWarc))
        {
            DateTime prev = DateTime.Now;
            foreach (WarcRecord record in reader)
            {
                foreach (var processor in processors)
                {
                    processor.ProcessRecord(record);
                }
                if (reader.RecordsRead % 100 == 0)
                {
                    Console.WriteLine($"{reader.Filename}\t{reader.RecordsRead}\t{Math.Truncate(DateTime.Now.Subtract(prev).TotalMilliseconds)} ms");
                    prev = DateTime.Now;
                }
            }
            foreach (var processor in processors)
            {
                processor.FinalizeProcessing();
            }
        }
    }

    private static string ResolveDir(string dir)
        => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
}