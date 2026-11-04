using System.Security.Cryptography;
using System.Text;
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
            bool replayMissingText=false;
            if (options.Search)
            {
                Console.WriteLine($"Preparing Kennedy2 database: {options.Database}");
                await using var db=await scope.ServiceProvider.GetRequiredService<IDbContextFactory<KennedyDbContext>>().CreateDbContextAsync(ct);
                await DatabaseMaintenance.InitializeAsync(db,ct);
                await using var missing=db.Database.GetDbConnection().CreateCommand();
                missing.CommandText="SELECT COUNT(*) FROM Documents d WHERE NOT EXISTS(SELECT 1 FROM DocumentsFts f WHERE f.rowid=d.Id);";
                replayMissingText=Convert.ToInt64(await missing.ExecuteScalarAsync(ct))>0;
                if(replayMissingText) Console.WriteLine("Missing full-text entries detected; supplied search inputs will be replayed automatically.");
            }
            using var archive=options.Archive ? new ArchiveImporter(archiveDirectory) : null;
            using var searchHistory=options.Search ? new ImportHistory(options.Database) : null;
            using var archiveHistory=options.Archive ? new ImportHistory(Path.Combine(archiveDirectory,"archive.db")) : null;
            Console.WriteLine($"Ready after {watch.Elapsed.TotalSeconds:F1}s. Search={options.Search}; archive={options.Archive}; {inputs.Count} input files.");
            int imported=0, skipped=0, failed=0;
            foreach (string input in inputs)
            {
                ct.ThrowIfCancellationRequested();
                var info=new FileInfo(input);
                string identity=Identity(info);
                bool search=options.Search && (replayMissingText || !searchHistory!.IsComplete(identity));
                bool saveArchive=options.Archive && !archiveHistory!.IsComplete(identity);
                if (!search && !saveArchive) { skipped++; Console.WriteLine($"Already complete: {input}"); continue; }
                try
                {
                    Console.WriteLine($"Importing {input} (search={search}, archive={saveArchive})");
                    var indexer=new WarcIndexer(search ? scope.ServiceProvider.GetRequiredService<ResponseStore>() : null,saveArchive ? archive : null);
                    await indexer.IndexFileAsync(input,ct);
                    if (Identity(new FileInfo(input))!=identity) throw new IOException("Input changed during import; use completed WARC files.");
                    if (search) searchHistory!.Complete(identity,info);
                    if (saveArchive) archiveHistory!.Complete(identity,info);
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
            Console.WriteLine($"Finished: {imported} imported, {skipped} skipped, {failed} failures; {watch.Elapsed.TotalSeconds:F1}s.");
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
    private static string Identity(FileInfo file) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"1|{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}")));
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
            foreach (var file in files) if (!File.Exists(file)) throw new FileNotFoundException("WARC not found",file);
            return files;
        }
    }

    private sealed class ImportHistory : IDisposable
    {
        private readonly SqliteConnection _connection;
        public ImportHistory(string path)
        {
            _connection=new SqliteConnection(ConnectionString(path));_connection.Open();
            using var command=_connection.CreateCommand();
            command.CommandText="CREATE TABLE IF NOT EXISTS WarcImports(Identity TEXT PRIMARY KEY,Path TEXT NOT NULL,Length INTEGER NOT NULL,ModifiedUtc TEXT NOT NULL,CompletedUtc TEXT NOT NULL);";
            command.ExecuteNonQuery();
        }
        public bool IsComplete(string identity)
        {
            using var command=_connection.CreateCommand();command.CommandText="SELECT 1 FROM WarcImports WHERE Identity=$id;";command.Parameters.AddWithValue("$id",identity);
            return command.ExecuteScalar()!=null;
        }
        public void Complete(string identity,FileInfo file)
        {
            using var command=_connection.CreateCommand();command.CommandText="INSERT OR REPLACE INTO WarcImports VALUES($id,$path,$length,$modified,$completed);";
            command.Parameters.AddWithValue("$id",identity);command.Parameters.AddWithValue("$path",file.FullName);command.Parameters.AddWithValue("$length",file.Length);
            command.Parameters.AddWithValue("$modified",file.LastWriteTimeUtc.ToString("O"));command.Parameters.AddWithValue("$completed",DateTime.UtcNow.ToString("O"));command.ExecuteNonQuery();
        }
        public void Dispose()=>_connection.Dispose();
    }
}
