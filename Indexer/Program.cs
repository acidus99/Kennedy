using System;
using System.IO;
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
        public static async Task<int> Main(string[] args)
        {
            string sqlitePath = "/Users/billy/kennedy-capsule/crawl-data/kennedy2.db";
            string languageConfigDir = "/Users/billy/Code/Kennedy/config-files/";

            //'/Users/billy/HDD Inside/Kennedy-Work/WARCs/2025-04-16.warc.gz'
            string[] warcFiles = ["/Users/billy/HDD Inside/Kennedy-Work/WARCs/2026-02-25.warc.gz",
                                    ];

            if (warcFiles.Length == 0)
            {
                Console.Error.WriteLine("You must provide at least one WARC file.");
                return 2;
            }

            foreach (var warc in warcFiles)
            {
                if (!File.Exists(warc))
                {
                    Console.Error.WriteLine($"WARC file not found: {warc}");
                    return 2;
                }
            }




            var services = new ServiceCollection();

            // Reuse the legacy NTextCat profile-driven detector from old Kennedy parsers.
            LanguageDetector.ConfigFileDirectory = languageConfigDir;

            services.AddDbContextFactory<KennedyDbContext>(options =>
            {
                options.UseSqlite($"Data Source={sqlitePath}");
            });

            services.AddScoped<ResponseStore>();
            services.AddScoped<FileSearchFtsRebuilder>();


            using var sp = services.BuildServiceProvider();
            await EnsureDatabaseCreatedAsync(sp, CancellationToken.None);

            await using var scope = sp.CreateAsyncScope();

            var responseStore = scope.ServiceProvider.GetRequiredService<ResponseStore>();

            var indexer = new WarcIndexer(responseStore);

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var warcFile in warcFiles)
            {
                Console.WriteLine($"Indexing: {warcFile}");
                await indexer.IndexFileAsync(warcFile, CancellationToken.None);
            }

            Console.WriteLine("Rebuilding file-search FTS...");
            var filesFtsRebuilder = scope.ServiceProvider.GetRequiredService<FileSearchFtsRebuilder>();
            await filesFtsRebuilder.RebuildAsync(CancellationToken.None);

            watch.Stop();

            Console.WriteLine($"Done. Elapsed {watch.Elapsed.TotalSeconds} seconds");

            if (args.Length >= 2 && args[0] == "--smoke-query")
            {
                await RunSmokeQueryAsync(sqlitePath, args[1], CancellationToken.None);
            }

            return 0;
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
