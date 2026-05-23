using System;
using System.IO;
using System.Threading;
using Kennedy.Data;
using Kennedy.Data.Services;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kennedy.Indexer
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            string sqlitePath = "/Users/billy/kennedy-capsule/crawl-data/kennedy2.db";

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

            services.AddDbContextFactory<KennedyDbContext>(options =>
            {
                options.UseSqlite($"Data Source={sqlitePath}");
            });

            services.AddScoped<UrlRegistryStore>(sp =>
            {
                var dbFactory = sp.GetRequiredService<IDbContextFactory<KennedyDbContext>>();
                return new UrlRegistryStore(dbFactory, batchSize: 3000);
            });


            using var sp = services.BuildServiceProvider();
            EnsureDatabaseCreatedAsync(sp, CancellationToken.None);

            await using var scope = sp.CreateAsyncScope();

            var store = scope.ServiceProvider.GetRequiredService<UrlRegistryStore>();

            var indexer = new WarcIndexer(store);

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var warcFile in warcFiles)
            {
                Console.WriteLine($"Indexing: {warcFile}");
                await indexer.IndexFileAsync(warcFile, CancellationToken.None);
            }

            watch.Stop();

            Console.WriteLine($"Done. Elapsed {watch.Elapsed.Seconds} seconds");
            return 0;
        }

        private static async Task EnsureDatabaseCreatedAsync(IServiceProvider sp, CancellationToken ct)
        {
            var dbFactory = sp.GetRequiredService<IDbContextFactory<KennedyDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            await db.Database.EnsureCreatedAsync(ct);
        }
    }
}
