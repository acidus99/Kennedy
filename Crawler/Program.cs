using Kennedy.Crawler.Crawling;

namespace Kennedy.Crawler;

class Program
{
    static void Main(string[] args)
    {
        HandleArgs(args);

        var crawler = new WebCrawler(40, 5000000);

        if (CrawlerOptions.SeedUrlsFile != "")
        {
            crawler.AddSeedsFromFile(CrawlerOptions.SeedUrlsFile);
        }
        else
        {
            crawler.AddSeed("gemini://mozz.us/");
            crawler.AddSeed("gemini://scarletthistle.farm");
            crawler.AddSeed("gemini://bbs.geminispace.org/");
            crawler.AddSeed("gemini://kennedy.gemi.dev/observatory/known-hosts");
            crawler.AddSeed("gemini://gemi.dev/");
        }

        crawler.DoCrawl();

        return;
    }

    static void HandleArgs(string[] args)
    {

        if (args.Length != 2)
        {
            Console.WriteLine("Usage: crawler [path to config] [path to output]");
            System.Environment.Exit(1);
        }

        string configPath = args[0];
        string outputPath = args[1];

        CrawlerOptions.ConfigDir = configPath;
        CrawlerOptions.OutputBase = outputPath;

        if (!CrawlerOptions.ConfigDir.EndsWith(Path.DirectorySeparatorChar))
        {
            CrawlerOptions.ConfigDir += Path.DirectorySeparatorChar;
        }

        // if (args.Length >= 1)
        // {
        //     CrawlerOptions.OutputBase = args[0];
        // }
        //
        // if (args.Length == 2)
        // {
        //     if (!File.Exists(args[1]))
        //     {
        //         throw new FileNotFoundException("Could not locate seed url file", args[1]);
        //     }
        //     CrawlerOptions.SeedUrlsFile = args[1];
        // }
        //
        // CrawlerOptions.OutputBase = CrawlerOptions.OutputBase.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
        if (!CrawlerOptions.OutputBase.EndsWith(Path.DirectorySeparatorChar))
        {
            CrawlerOptions.OutputBase += Path.DirectorySeparatorChar;
        }
        if (!Directory.Exists(CrawlerOptions.OutputBase))
        {
            Directory.CreateDirectory(CrawlerOptions.OutputBase);
        }

        Console.WriteLine($"Using '{CrawlerOptions.ConfigDir}' as config directory.");
        Console.WriteLine($"Using '{CrawlerOptions.OutputBase}' as output directory.");
    }
}
