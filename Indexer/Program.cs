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
            string sqlitePath = "/Users/billy/kennedy-capsule/crawl-data/kennedy2.db";
            string languageConfigDir = "/Users/billy/Code/Kennedy/config-files/";

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

            // Bootstrap mode: two-phase over a directory of WARCs.
            //   indexer --bootstrap /Volumes/WARC-BACKUP/WARCs/
            bool bootstrapMode = args.Length >= 2 && args[0] == "--bootstrap";
            string? bootstrapDir = bootstrapMode ? args[1] : null;

            // Single-WARC mode (default). Edit the list below as needed.
            string[] singleWarcFiles = bootstrapMode ? [] :
            [
                "/Users/billy/HDD Inside/Kennedy-Work/WARCs/2026-02-25.warc.gz",
            ];

            if (!bootstrapMode)
            {
                foreach (var warc in singleWarcFiles)
                {
                    if (!File.Exists(warc))
                    {
                        Console.Error.WriteLine($"WARC file not found: {warc}");
                        return 2;
                    }
                }
            }

            var services = new ServiceCollection();
            LanguageDetector.ConfigFileDirectory = languageConfigDir;

            services.AddDbContextFactory<KennedyDbContext>(options =>
                options.UseSqlite($"Data Source={sqlitePath}"));

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
                await RunBootstrapAsync(indexer, bootstrapDir!, CancellationToken.None);
            }
            else
            {
                foreach (var warcFile in singleWarcFiles)
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

            if (args.Length >= 2 && args[0] == "--smoke-query")
            {
                await RunSmokeQueryAsync(sqlitePath, args[1], CancellationToken.None);
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
                    // Strip both extensions: "2025-04-16.warc.gz" → "2025-04-16"
                    var stem = Path.GetFileNameWithoutExtension(
                                   Path.GetFileNameWithoutExtension(p));
                    DateTime.TryParseExact(
                        stem, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var date);
                    return (path: p, date);
                })
                .Where(x => x.date != default)
                .OrderBy(x => x.date)
                .ToList();
        }

        private static async Task EnsureDatabaseCreatedAsync(IServiceProvider sp, CancellationToken ct)
        {
            var dbFactory = sp.GetRequiredService<IDbContextFactory<KennedyDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            await db.Database.EnsureCreatedAsync(ct);
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
                FROM DocumentsFts f
                JOIN Documents d ON d.Id = f.rowid
                WHERE f MATCH $query
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
    }
}
