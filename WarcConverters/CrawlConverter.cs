namespace Kennedy.WarcConverters;

using System;
using WarcDotNet;
using Kennedy.Warc;

/// <summary>
/// Converts legacy crawls into WARCs
/// </summary>
public static class CrawlConverter
{
    public static void Convert()
    {
        string workingDir = ResolveDir("~/HDD Inside/Kennedy-Work/");

        if (workingDir.Length == 0)
        {
            throw new ApplicationException("Set working directory!");
        }

        string warcOutputDir = workingDir + "WARCs/";
        string sourceDir = workingDir + "pre-WARCs/";

        ImportMozzPortalContent(warcOutputDir + "mozz-extracted-content.warc", sourceDir + "mozz-urls.txt");

        //ImportCrawl(ConverterType.LegacyA, warcOutputDir, sourceDir + "legacy-A/manifest.txt");
        //ImportCrawl(ConverterType.LegacyB, warcOutputDir, sourceDir + "legacy-B/manifest.txt");
        //ImportCrawl(ConverterType.LegacyC, warcOutputDir, sourceDir + "legacy-C/manifest.txt");
        //ImportCrawl(ConverterType.CrawlDb, warcOutputDir, sourceDir + "crawldb/manifest.txt");
    }

    static void ReadWarc()
    {
        WarcStats stats = new WarcStats();

        foreach (var file in Directory.GetFiles(ResolveDir("~/HDD Inside/Kennedy-Work/WARCs"), "*.warc").OrderBy(x => x))
        {
            string filename = Path.GetFileName(file);
            Console.WriteLine("Working on " + file);
            stats.Scan(file);
        }
        stats.WriteResults(ResolveDir("~/HDD Inside/Kennedy-Work/mime-stats.csv"));
    }


    /// <summary>
    /// Using a purpose-built function to better control WARC fields
    /// </summary>
    /// <param name="type"></param>
    /// <param name="warcOutputDir"></param>
    /// <param name="manifest"></param>
    static void ImportMozzPortalContent(string warcOutputFile, string urlsInputFile)
    {
        using (var warcCreator = new GeminiWarcCreator(warcOutputFile))
        {
            warcCreator.WriteWarcInfo(new WarcFields
            {
                {"software", "Kennedy Mozz Portal Importer"},
                {"details", "This WARC contains connect extract and converted from the Internet Archive"},
                {"hostname", "kennedy.gemi.dev"},
                {"created", DateTime.Now },
                {"operator", "Acidus"}
            });

            var converter = new MozzPortalArchiveConverter(warcCreator, urlsInputFile);
            converter.WriteToWarc();
        }
    }

    static void ImportCrawl(ConverterType type, string warcOutputDir, string manifest)
    {
        foreach (string crawlLocation in File.ReadLines(manifest))
        {
            var warcFile = CreateWarcName(crawlLocation);
            using (var warcCreator = new GeminiWarcCreator(warcOutputDir + warcFile))
            {
                warcCreator.WriteWarcInfo(GetWarcFields());
                var converter = GetConverter(type, warcCreator, crawlLocation);
                converter.WriteToWarc();
            }
        }
    }

    static AbstractConverter GetConverter(ConverterType type, GeminiWarcCreator warcCreator, string crawlLocation)
    {
        switch (type)
        {
            case ConverterType.LegacyA:
                return new LegacyAConverter(warcCreator, crawlLocation);

            case ConverterType.LegacyB:
                return new LegacyBConverter(warcCreator, crawlLocation);

            case ConverterType.LegacyC:
                return new LegacyCConverter(warcCreator, crawlLocation);

            case ConverterType.CrawlDb:
                return new CrawlDbConverter(warcCreator, crawlLocation);
        }

        throw new ArgumentException("Unknown Converter Type", nameof(type));
    }

    static WarcFields GetWarcFields()
        => new WarcFields
                {
                    {"software", "Kennedy Legacy Crawl importer"},
                    {"hostname", "kennedy.gemi.dev"},
                    {"operator", "Acidus"}
                };

    static string CreateWarcName(string crawlLocation)
    {
        return Path.GetDirectoryName(crawlLocation)!.Split(Path.DirectorySeparatorChar).Reverse().First() + ".warc";
    }

    private static string ResolveDir(string dir)
        => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
}

