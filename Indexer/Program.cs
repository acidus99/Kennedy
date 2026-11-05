using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mono.Options;

namespace Kennedy.Indexer;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_,e) => { e.Cancel=true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        var locks = new List<FileStream>();
        try
        {
            var options = Options.Parse(args);
            if (options.Help) { Usage(); return 0; }
            var inputs = options.ResolveInputs();
            string archiveDirectory = options.OutputDirectory;
            var destinations = new List<string>();
            if (options.Search) destinations.Add(options.Database);
            if (options.Archive) destinations.Add(Path.Combine(archiveDirectory,"archive.db"));
            foreach (string destination in destinations.Distinct().OrderBy(p=>p,StringComparer.Ordinal))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                locks.Add(new FileStream(destination+".indexer.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None));
            }
            var ct = cancellation.Token;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var services = new ServiceCollection();
            LanguageDetector.ConfigFileDirectory=Path.Combine(AppContext.BaseDirectory,"config")+Path.DirectorySeparatorChar;
            services.AddDbContextFactory<KennedyDbContext>(db => db.UseSqlite(ConnectionString(options.Database)));
            services.AddScoped<ResponseStore>();
            services.AddScoped<FileSearchFtsRebuilder>();
            services.AddScoped<SearchPolicyUpdater>();
            using var provider=services.BuildServiceProvider();
            await using var scope=provider.CreateAsyncScope();
            if (options.Search)
            {
                Console.WriteLine($"Preparing Kennedy2 database: {options.Database}");
                await using var db=await scope.ServiceProvider.GetRequiredService<IDbContextFactory<KennedyDbContext>>().CreateDbContextAsync(ct);
                await DatabaseMaintenance.InitializeAsync(db,ct);
            }
            using var archive=options.Archive ? new ArchiveImporter(archiveDirectory) : null;
            Console.WriteLine($"Ready after {watch.Elapsed.TotalSeconds:F1}s. Search={options.Search}; archive={options.Archive}; {inputs.Count} input files.");
            int imported=0, failed=0;
            foreach (string input in inputs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    Console.WriteLine($"Importing {input} (search={options.Search}, archive={options.Archive})");
                    var indexer=new WarcIndexer(options.Search ? scope.ServiceProvider.GetRequiredService<ResponseStore>() : null,archive);
                    await indexer.IndexFileAsync(input,ct);
                    imported++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    Console.Error.WriteLine($"FAILED {input}: {ex.Message}");
                }
            }
            if (options.Search)
            {
                Console.WriteLine("Applying robots policy and finishing pending file-search work...");
                await scope.ServiceProvider.GetRequiredService<SearchPolicyUpdater>().ApplyAsync(ct);
                await scope.ServiceProvider.GetRequiredService<FileSearchFtsRebuilder>().RebuildAsync(ct);
                await using var db=await scope.ServiceProvider.GetRequiredService<IDbContextFactory<KennedyDbContext>>().CreateDbContextAsync(ct);
                await db.Database.OpenConnectionAsync(ct);
                await using var check=db.Database.GetDbConnection().CreateCommand();
                check.CommandText="SELECT COUNT(*) FROM Documents d WHERE NOT EXISTS(SELECT 1 FROM DocumentsFts f WHERE f.rowid=d.Id);";
                long missing=Convert.ToInt64(await check.ExecuteScalarAsync(ct));
                if (missing>0)
                {
                    Console.Error.WriteLine($"{missing:N0} text documents need their original WARC responses. Supply the relevant WARCs to restore their missing full-text entries.");
                    failed++;
                }
            }
            archive?.Complete(ct);
            Console.WriteLine($"Finished: {imported} files processed, {failed} failures; {watch.Elapsed.TotalSeconds:F1}s.");
            return failed==0 ? 0 : 1;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Interrupted. Committed batches and pending work are preserved; rerun the command to resume."); return 130; }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
        finally
        {
            Console.CancelKeyPress -= cancel;
            foreach (var file in locks) file.Dispose();
        }
    }

    internal static string ConnectionString(string path) => new SqliteConnectionStringBuilder { DataSource=path }.ToString();
    private static void Usage() => Console.WriteLine("""
        Usage: dotnet run --project Indexer -c Release -- [options] <WARC> [WARC...]
          -a, --archive        Index input into the archive.
          -s, --search         Index input into search.
          -o, --output <dir>   Output directory (required).
          -h, --help           Show this message and exit.
        Combine -s and -a to update both. All selected post-processing runs automatically.
        """);

    private sealed class Options
    {
        public string OutputDirectory="";
        public string Database => Path.Combine(OutputDirectory,"kennedy2.db");
        public bool Search,Archive,Help;
        private readonly List<string> _files=[];
        public static Options Parse(string[] args)
        {
            var options=new Options();
            var parser=new OptionSet
            {
                { "a|archive", "index input into the archive", _ => options.Archive=true },
                { "s|search", "index input into search", _ => options.Search=true },
                { "o|output=", "output location", value => options.OutputDirectory=value ?? "" },
                { "h|help", "show help", value => options.Help=value!=null }
            };
            options._files.AddRange(parser.Parse(args));
            var unknown=options._files.FirstOrDefault(file=>file.StartsWith('-'));
            if (unknown!=null) throw new ArgumentException($"Unknown option: {unknown}");
            return options;
        }
        public List<string> ResolveInputs()
        {
            if (!Search && !Archive)
                throw new ArgumentException("Select search (-s or --search), archive (-a or --archive), or both.");
            if (string.IsNullOrEmpty(OutputDirectory))
                throw new ArgumentException("Specify the output directory (-o DIRECTORY or --output DIRECTORY).");
            OutputDirectory=Path.GetFullPath(OutputDirectory);
            if (!Directory.Exists(OutputDirectory))
                throw new DirectoryNotFoundException($"Cannot read output directory '{OutputDirectory}'.");
            if (_files.Count==0) throw new ArgumentException("Supply at least one WARC file.");
            var files=_files.Select(Path.GetFullPath).ToList();
            foreach (var file in files)
            {
                if (!File.Exists(file)) throw new FileNotFoundException("WARC not found",file);
                WarcResponseReader.ValidateInput(file);
            }
            return files;
        }
    }

}
