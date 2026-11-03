using System.Security.Cryptography;
using System.Text;
using Gemini.Net;
using Kennedy.Data;
using Kennedy.Data.Parsers;
using Kennedy.Data.Services;
using Kennedy.Search.Query;
using Kennedy.Search.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
            if (args.Length == 3 && args[0] == "--cert-list")
            {
                await new WarcIndexer(null).WriteCertificateListCsvAsync(args[1],args[2],cancellation.Token);
                return 0;
            }
            var options = Options.Parse(args);
            if (options.Help) { Usage(); return 0; }
            var inputs = options.ResolveInputs();
            if (inputs.Count == 0 && !options.Maintenance) throw new ArgumentException("Supply WARC files, --warc-dir, or --maintenance.");
            string archiveDirectory = options.ArchiveDirectory ?? Path.GetDirectoryName(options.Database)!;
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
            LanguageDetector.ConfigFileDirectory=options.ConfigDirectory;
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
            using var searchHistory=options.Search ? new ImportHistory(options.Database) : null;
            using var archiveHistory=options.Archive ? new ImportHistory(Path.Combine(archiveDirectory,"archive.db")) : null;
            Console.WriteLine($"Ready after {watch.Elapsed.TotalSeconds:F1}s. Search={options.Search}; archive={options.Archive}; {inputs.Count} input files.");
            int imported=0, skipped=0, failed=0;
            foreach (string input in inputs)
            {
                ct.ThrowIfCancellationRequested();
                var info=new FileInfo(input);
                string identity=Identity(info);
                bool search=options.Search && (options.Force || !searchHistory!.IsComplete(identity));
                bool saveArchive=options.Archive && (options.Force || !archiveHistory!.IsComplete(identity));
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
                    Console.Error.WriteLine($"{missing:N0} text documents need their original WARC responses. Replay the relevant WARCs with --force to restore their missing full-text entries.");
                    failed++;
                }
            }
            archive?.Complete(ct);
            if (options.SmokeQuery!=null && options.Search)
            {
                var query=new QueryParser().Parse(options.SmokeQuery);
                var search=new SqliteSearchService(options.Database);
                Console.WriteLine($"Search '{options.SmokeQuery}': {search.GetTextResultsCount(query):N0} results");
                foreach (var result in search.SearchText(query,0,5)) Console.WriteLine($"- {result.Url} {result.Title}");
            }
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
        Usage: dotnet run --project Indexer -- [options] <WARC> [WARC...]
          --warc-dir <dir>      Import .warc and .warc.gz files, including suffixed names.
          --bootstrap <dir>     Alias for --warc-dir; no age cutoff.
          --db <path>           Kennedy2 search DB (default: ~/kennedy-capsule/crawl-data/kennedy2.db).
          --archive-dir <dir>   archive.db and Packs location (default: search DB directory).
          --both               Update search and archive (default).
          --search-only        Update search only (--search is an alias).
          --archive-only       Update archive only (--archive is an alias).
          --config-dir <dir>    Directory containing Core14.profile.xml.
          --maintenance        Repair schema and finish pending work; WARC inputs optional.
          --force              Replay completed inputs, for backfill/repair.
          --smoke-query <text>  Show search count and five results after processing.
          --cert-list <warc> <csv>  Export captured certificates without importing.
        Exit codes: 0 complete; 1 partial/import failures; 2 setup error; 130 interrupted.
        """);

    private sealed class Options
    {
        public string Database=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"kennedy-capsule","crawl-data","kennedy2.db");
        public string? ArchiveDirectory,SmokeQuery;
        public string ConfigDirectory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../config-files"))+Path.DirectorySeparatorChar;
        public bool Search=true,Archive=true,Force,Maintenance,Help;
        private readonly List<string> _files=[],_directories=[];
        public static Options Parse(string[] args)
        {
            var options=new Options();
            for (int i=0;i<args.Length;i++)
            {
                string arg=args[i];
                string Value() => i+1<args.Length && !args[i+1].StartsWith("--") ? args[++i] : throw new ArgumentException($"{arg} requires a value.");
                switch (arg)
                {
                    case "-h": case "--help": options.Help=true; break;
                    case "--both": options.Search=options.Archive=true; break;
                    case "--search": case "--search-only": options.Search=true;options.Archive=false;break;
                    case "--archive": case "--archive-only": options.Archive=true;options.Search=false;break;
                    case "--force": options.Force=true;break;
                    case "--maintenance": options.Maintenance=true;break;
                    case "--db": options.Database=Value();break;
                    case "--archive-dir": options.ArchiveDirectory=Path.GetFullPath(Value());break;
                    case "--config-dir": options.ConfigDirectory=Path.GetFullPath(Value())+Path.DirectorySeparatorChar;break;
                    case "--smoke-query": options.SmokeQuery=Value();break;
                    case "--bootstrap": case "--warc-dir": options._directories.Add(Value());break;
                    default:
                        if (arg.StartsWith('-')) throw new ArgumentException($"Unknown option: {arg}");
                        options._files.Add(arg);break;
                }
            }
            options.Database=Path.GetFullPath(options.Database);
            if (Path.GetFileName(options.Database).Equals("archive.db",StringComparison.OrdinalIgnoreCase) && options.Search)
                throw new ArgumentException("The search database cannot be archive.db.");
            return options;
        }
        public List<string> ResolveInputs()
        {
            var files=new List<string>(_files);
            foreach (var dir in _directories)
            {
                if (!Directory.Exists(dir)) throw new DirectoryNotFoundException(dir);
                files.AddRange(Directory.EnumerateFiles(dir).Where(f => f.EndsWith(".warc",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".warc.gz",StringComparison.OrdinalIgnoreCase)));
            }
            var result=files.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).OrderBy(f=>Path.GetFileName(f),StringComparer.Ordinal).ToList();
            foreach (var file in result) if (!File.Exists(file)) throw new FileNotFoundException("WARC not found",file);
            return result;
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
