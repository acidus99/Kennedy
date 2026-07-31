using System;
using System.IO;
using System.Linq;
using System.Threading;
using Kennedy.Data;
using Kennedy.Data.Services;
using Kennedy.Data.Parsers;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kennedy.Indexer
{
    internal static class Program
    {
        // WARCs older than this are skipped in Phase 2 (full index).
        static readonly TimeSpan RecentWarcCutoff = TimeSpan.FromDays(180);

        public static async Task<int> Main(string[] args)
        {
            bool certListMode = args.Length >= 3 && args[0] == "--cert-list";
            string? certListWarcPath = certListMode ? args[1] : null;
            string? certListOutputPath = certListMode ? args[2] : null;

            if (certListMode)
            {
                if (!File.Exists(certListWarcPath))
                {
                    Console.Error.WriteLine($"WARC file not found: {certListWarcPath}");
                    return 2;
                }

                var certListIndexer = new WarcIndexer(responseStore: null!);
                Console.WriteLine($"Building certificate list: {certListWarcPath}");
                await certListIndexer.WriteCertificateListCsvAsync(certListWarcPath!, certListOutputPath!, CancellationToken.None);
                Console.WriteLine($"Wrote certificate CSV: {certListOutputPath}");
                return 0;
            }

            IndexerOptions options;
            try
            {
                options = ParseOptions(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                DisplayUsage();
                return 2;
            }
            if (options.ShowHelp)
            {
                DisplayUsage();
                return 0;
            }

            // Bootstrap mode: two-phase over a directory of WARCs.
            //   indexer --bootstrap /Volumes/WARC-BACKUP/WARCs/
            bool bootstrapMode = !string.IsNullOrWhiteSpace(options.BootstrapDir);
            IReadOnlyList<string> warcFiles = [];
            if (!bootstrapMode)
            {
                warcFiles = ResolveWarcInputs(options);
                if (warcFiles.Count == 0)
                {
                    Console.Error.WriteLine("No WARC files provided.");
                    DisplayUsage();
                    return 2;
                }
            }

            if (bootstrapMode && options.WarcFiles.Count > 0)
            {
                Console.Error.WriteLine("Do not combine --bootstrap with positional WARC files.");
                return 2;
            }

            var services = new ServiceCollection();
            LanguageDetector.ConfigFileDirectory = options.LanguageConfigDir;

            services.AddDbContextFactory<KennedyDbContext>(dbOptions =>
                dbOptions.UseSqlite($"Data Source={options.SqlitePath}"));

            services.AddScoped<ResponseStore>();
            services.AddScoped<FileSearchFtsRebuilder>();

            using var sp = services.BuildServiceProvider();
            await EnsureDatabaseCreatedAsync(sp, CancellationToken.None);

            await using var scope = sp.CreateAsyncScope();
            var responseStore = scope.ServiceProvider.GetRequiredService<ResponseStore>();
            var indexer = new WarcIndexer(responseStore);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (bootstrapMode)
            {
                await RunBootstrapAsync(indexer, options.BootstrapDir!, CancellationToken.None);
            }
            else
            {
                Console.WriteLine($"Indexing {warcFiles.Count} WARC file(s).");
                foreach (var warcFile in warcFiles)
                {
                    Console.WriteLine($"Indexing: {warcFile}");
                    await indexer.IndexFileAsync(warcFile, CancellationToken.None);
                }
            }

            Console.WriteLine("Rebuilding file-search FTS...");
            var filesFtsRebuilder = scope.ServiceProvider.GetRequiredService<FileSearchFtsRebuilder>();
            await filesFtsRebuilder.RebuildAsync(CancellationToken.None);

            stopwatch.Stop();
            Console.WriteLine($"Done. Elapsed {stopwatch.Elapsed.TotalSeconds:F1} seconds");

            if (!string.IsNullOrWhiteSpace(options.SmokeQuery))
            {
                await RunSmokeQueryAsync(options.SqlitePath, options.SmokeQuery, CancellationToken.None);
            }

            return 0;
        }

        private static async Task RunBootstrapAsync(WarcIndexer indexer, string warcDir, CancellationToken ct)
        {
            var allWarcs = GetWarcsSortedByDate(warcDir);
            if (allWarcs.Count == 0)
            {
                Console.Error.WriteLine($"No dated WARC files (yyyy-MM-dd.warc.gz) found in: {warcDir}");
                return;
            }

            var cutoff = DateTime.UtcNow - RecentWarcCutoff;
            var recentWarcs = allWarcs.Where(w => w.date >= cutoff).ToList();

            Console.WriteLine($"Bootstrap: {allWarcs.Count} total WARCs, {recentWarcs.Count} within the last 6 months");
            Console.WriteLine($"Cutoff date: {cutoff:yyyy-MM-dd}");

            // Phase 1: All WARCs → UrlRegistry only.
            // Order doesn't matter — "latest wins" semantics prevent older WARCs from
            // overwriting a more recent status already recorded by a later WARC.
            Console.WriteLine();
            Console.WriteLine("=== Phase 1: UrlRegistry (all WARCs) ===");
            int p1 = 0;
            foreach (var (path, date) in allWarcs)
            {
                Console.WriteLine($"[{++p1}/{allWarcs.Count}] {Path.GetFileName(path)} ({date:yyyy-MM-dd})");
                await indexer.IndexFileRegistryOnlyAsync(path, ct);
            }

            // Phase 2: Recent WARCs → full index (Documents, Images, FTS, Links).
            // Processed in chronological order so the most recent content wins.
            Console.WriteLine();
            Console.WriteLine("=== Phase 2: Full index (recent WARCs) ===");
            int p2 = 0;
            foreach (var (path, date) in recentWarcs)
            {
                Console.WriteLine($"[{++p2}/{recentWarcs.Count}] {Path.GetFileName(path)} ({date:yyyy-MM-dd})");
                await indexer.IndexFileAsync(path, ct);
            }
        }

        /// <summary>
        /// Returns all *.warc.gz files in <paramref name="dir"/> whose name parses as yyyy-MM-dd,
        /// sorted chronologically oldest-first.
        /// </summary>
        private static IReadOnlyList<(string path, DateTime date)> GetWarcsSortedByDate(string dir)
        {
            return Directory.EnumerateFiles(dir, "*.warc.gz", SearchOption.TopDirectoryOnly)
                .Select(p =>
                {
                    var date = TryParseWarcDate(p) ?? default;
                    return (path: p, date);
                })
                .Where(x => x.date != default)
                .OrderBy(x => x.date)
                .ThenBy(x => x.path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<string> ResolveWarcInputs(IndexerOptions options)
        {
            var files = new List<string>();
            files.AddRange(options.WarcFiles);

            foreach (var dir in options.WarcDirs)
            {
                if (!Directory.Exists(dir))
                {
                    Console.Error.WriteLine($"WARC directory not found: {dir}");
                    return [];
                }

                files.AddRange(Directory.EnumerateFiles(dir, "*.warc", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.EnumerateFiles(dir, "*.warc.gz", SearchOption.TopDirectoryOnly));
            }

            var missing = files.Where(f => !File.Exists(f)).ToList();
            if (missing.Count > 0)
            {
                foreach (var file in missing)
                {
                    Console.Error.WriteLine($"WARC file not found: {file}");
                }
                return [];
            }

            return files
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => TryParseWarcDate(f) ?? DateTime.MaxValue)
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static DateTime? TryParseWarcDate(string path)
        {
            var fileName = Path.GetFileName(path);
            if (fileName.Length < 10)
            {
                return null;
            }

            var dateText = fileName[..10];
            return DateTime.TryParseExact(
                dateText,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date)
                    ? date
                    : null;
        }

        private static async Task EnsureDatabaseCreatedAsync(IServiceProvider sp, CancellationToken ct)
        {
            var dbFactory = sp.GetRequiredService<IDbContextFactory<KennedyDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            await db.Database.EnsureCreatedAsync(ct);
            await db.EnsureSchemaCompatibilityAsync(ct);
            await db.EnsureFtsAsync(ct);
            await db.ApplyPerformancePragmasAsync(ct);
        }

        private static async Task RunSmokeQueryAsync(string sqlitePath, string query, CancellationToken ct)
        {
            Console.WriteLine($"FTS smoke query: {query}");

            await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT d.CanonicalUrl, d.Title
                FROM DocumentsFts
                JOIN Documents d ON d.Id = DocumentsFts.rowid
                WHERE DocumentsFts MATCH $query
                LIMIT 5;
                """;
            cmd.Parameters.AddWithValue("$query", query);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var url = reader.GetString(0);
                var title = reader.IsDBNull(1) ? "" : reader.GetString(1);
                Console.WriteLine($"- {title} [{url}]");
            }
        }

        private static IndexerOptions ParseOptions(string[] args)
        {
            var options = new IndexerOptions();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;
                    case "--bootstrap":
                        options.BootstrapDir = RequireValue(args, ref i, arg);
                        break;
                    case "--warc-dir":
                        options.WarcDirs.Add(RequireValue(args, ref i, arg));
                        break;
                    case "--db":
                        options.SqlitePath = RequireValue(args, ref i, arg);
                        break;
                    case "--config-dir":
                        options.LanguageConfigDir = RequireValue(args, ref i, arg);
                        break;
                    case "--smoke-query":
                        options.SmokeQuery = RequireValue(args, ref i, arg);
                        break;
                    default:
                        if (arg.StartsWith("--", StringComparison.Ordinal))
                        {
                            throw new ArgumentException($"Unknown option: {arg}");
                        }
                        options.WarcFiles.Add(arg);
                        break;
                }
            }

            return options;
        }

        private static string RequireValue(string[] args, ref int index, string optionName)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{optionName} requires a value.");
            }

            return args[++index];
        }

        private static void DisplayUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project Indexer -- [options] <warc-file> [warc-file...]");
            Console.WriteLine("  dotnet run --project Indexer -- --warc-dir <directory>");
            Console.WriteLine("  dotnet run --project Indexer -- --bootstrap <directory>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --warc-dir <dir>       Add all .warc and .warc.gz files in a directory.");
            Console.WriteLine("  --bootstrap <dir>      Two-phase import: all WARCs into UrlRegistry, recent WARCs into search.");
            Console.WriteLine("  --db <path>            SQLite database path.");
            Console.WriteLine("  --config-dir <dir>     Language profile config directory.");
            Console.WriteLine("  --smoke-query <query>  Run a smoke FTS query after indexing.");
            Console.WriteLine("  --cert-list <warc> <csv> remains available as a separate mode.");
        }

        private sealed class IndexerOptions
        {
            public string SqlitePath { get; set; } = "/Users/billy/kennedy-capsule/crawl-data/kennedy2.db";
            public string LanguageConfigDir { get; set; } = "/Users/billy/Code/Kennedy/config-files/";
            public string? BootstrapDir { get; set; }
            public string? SmokeQuery { get; set; }
            public bool ShowHelp { get; set; }
            public List<string> WarcDirs { get; } = [];
            public List<string> WarcFiles { get; } = [];
        }
    }
}
