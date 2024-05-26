using System.Diagnostics;
using Kennedy.Indexer.WarcProcessors;
using Mono.Options;
using WarcDotNet;

namespace Kennedy.Indexer;
class Program
{

    static string ProgramName
        => Process.GetCurrentProcess().MainModule?.FileName ?? "Program";

    /// <summary>
    /// Consumes a WARC and generates a search index
    /// </summary>
    /// <param name="args"></param>
    static void Main(string[] args)
    {
        IndexerOptions options = ParseOptions(args);
        ValidateOptions(options);
        DisplaySummary(options);

        GeminiWarcProcessor processor = CreateProcessors(options);

        foreach (var inputWarc in options.InputWarcs)
        {
            ProcessWarc(inputWarc, processor);
        }

    }

    /// <summary>
    /// Creates needed processors for the given options
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    static GeminiWarcProcessor CreateProcessors(IndexerOptions options)
    {
        string configDir = ResolveDir("~/kennedy-capsule/config/");
        GeminiWarcProcessor processor = new GeminiWarcProcessor(configDir);
        if (options.ShouldIndexArchive)
        {
            processor.RecordProcessors.Add(new ArchiveProcessor(options.OutputLocation));
        }
        if (options.ShouldIndexCrawl)
        {
            processor.RecordProcessors.Add(new SearchProcessor(options.OutputLocation, configDir));
        }
        return processor;
    }

    static void DisplayError(string errorMsg)
    {
        Console.Error.WriteLine(errorMsg);
        Environment.Exit(1);
    }

    static void DisplaySummary(IndexerOptions options)
    {
        Console.WriteLine(ProgramName);
        Console.WriteLine($"Indexing {options.InputWarcs.Count} file(s)");
        Console.WriteLine($"Index location\t'{options.OutputLocation}'");
        Console.Write("Processors:\t");
        if (options.ShouldIndexArchive)
        {
            Console.Write("archiver ");
        }
        if (options.ShouldIndexCrawl)
        {
            Console.Write("search ");
        }
        Console.WriteLine();
    }

    static IndexerOptions ParseOptions(string[] args)
    {
        var ret = new IndexerOptions();
        bool showHelp = false;

        var options = new OptionSet {
            { "a|archive", "index input into the archive", a => ret.ShouldIndexArchive = true },
            { "o|output=", "output location", o => ret.OutputLocation = o ?? "" },
            { "s|search", "index input into search", s => ret.ShouldIndexCrawl = true },
            { "h|help", "show this message and exit", h => showHelp = h != null },
        };

        ret.InputWarcs.AddRange(options.Parse(args));

        if (showHelp)
        {
            Console.WriteLine(ProgramName);
            options.WriteOptionDescriptions(Console.Out);
            Environment.Exit(0);
        }

        return ret;
    }

    static void ProcessWarc(string inputWarc, GeminiWarcProcessor processor)
    {
        using (WarcReader reader = new WarcReader(inputWarc))
        {
            DateTime start = DateTime.Now;
            DateTime prev = start;
            foreach (WarcRecord record in reader)
            {
                processor.ProcessRecord(record);
                if (reader.RecordsRead % 100 == 0)
                {
                    var elapsedSeconds = Math.Truncate(DateTime.Now.Subtract(start).TotalSeconds);
                    var ratePerSecond = Math.Truncate(reader.RecordsRead / elapsedSeconds);
                    Console.Write($"{reader.Filename}\t{reader.RecordsRead}\t {elapsedSeconds} s ({ratePerSecond} / s)    ");
                    Console.Write('\r');
                    prev = DateTime.Now;
                }
            }
            Console.WriteLine();
            Console.WriteLine("Post processing");
            processor.FinalizeProcessing();
        }
    }

    private static string ResolveDir(string dir)
        => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');

    static void ValidateOptions(IndexerOptions options)
    {
        if (!options.ShouldIndexArchive && !options.ShouldIndexCrawl)
        {
            DisplayError("Must include an option to index for search (-s or --search), for archive (-a or --archive), or for both.");
        }

        if (string.IsNullOrEmpty(options.OutputLocation))
        {
            DisplayError("Must include an option for the output location (-o DIRECTORY or --output DIRECTORY).");
        }

        if (!Directory.Exists(options.OutputLocation))
        {
            DisplayError($"Cannot read output directory '{options.OutputLocation}'.");
        }

        if (options.InputWarcs.Count < 1)
        {
            DisplayError("Must include at least 1 WARC file to process. e.g. Indexer -s -o /some/directory/ some-file.warc");
        }

        foreach (var inputWarc in options.InputWarcs)
        {
            if (!File.Exists(inputWarc))
            {
                DisplayError($"Cannot read input WARC file '{inputWarc}'");
            }
        }
    }
}