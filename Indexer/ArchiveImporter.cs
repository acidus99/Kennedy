using System.Text.Json;
using Gemini.Net;
using Kennedy.Archive;
using Kennedy.Data.RobotsTxt;
using Kennedy.Data.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Indexer;

/// <summary>Archive writes, durable visibility work, and archive statistics.</summary>
public sealed class ArchiveImporter : IDisposable
{
    private readonly string _directory;
    private readonly Archiver _archiver;
    private readonly SqliteConnection _connection;

    public ArchiveImporter(string directory)
    {
        _directory=Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
        string dbPath=Path.Combine(_directory,"archive.db");
        _archiver=new Archiver(dbPath,Path.Combine(_directory,"Packs"));
        _connection=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=dbPath }.ToString());
        _connection.Open();
        using var command=_connection.CreateCommand();
        command.CommandText="""
            PRAGMA journal_mode=WAL;
            CREATE INDEX IF NOT EXISTS IX_Snapshots_UrlId_Captured ON Snapshots(UrlId,Captured);
            CREATE TABLE IF NOT EXISTS ArchiveWorkItems(Scheme TEXT,Host TEXT,Port INTEGER,PRIMARY KEY(Scheme,Host,Port));
            CREATE TABLE IF NOT EXISTS ArchiveRobotsPolicies(Scheme TEXT,Host TEXT,Port INTEGER,Captured TEXT NOT NULL,Content TEXT NOT NULL,PRIMARY KEY(Scheme,Host,Port));
            """;
        command.ExecuteNonQuery();
    }

    public int StoreBatch(IReadOnlyList<GeminiResponse> responses, CancellationToken ct)
    {
        // Persist the work before writing captures. Interrupted imports are replayable;
        // new URLs remain hidden until visibility processing has completed.
        using (var transaction=_connection.BeginTransaction())
        {
            foreach (var group in responses.GroupBy(r => (r.RequestUrl.Protocol,r.RequestUrl.Hostname,r.RequestUrl.Port)))
            {
                using var command=_connection.CreateCommand();
                command.Transaction=transaction;
                command.CommandText="INSERT OR IGNORE INTO ArchiveWorkItems VALUES($scheme,$host,$port);";
                command.Parameters.AddWithValue("$scheme",group.Key.Protocol);
                command.Parameters.AddWithValue("$host",group.Key.Hostname);
                command.Parameters.AddWithValue("$port",group.Key.Port);
                command.ExecuteNonQuery();
            }
            foreach (var response in responses.Where(r => UrlUtility.IsRobotsUrl(r.RequestUrl.Url) && !r.IsConnectionError && !r.IsTempFail))
            {
                using var command=_connection.CreateCommand();
                command.Transaction=transaction;
                command.CommandText="""
                    INSERT INTO ArchiveRobotsPolicies VALUES($scheme,$host,$port,$captured,$content)
                    ON CONFLICT(Scheme,Host,Port) DO UPDATE SET Captured=excluded.Captured,Content=excluded.Content
                    WHERE excluded.Captured>=ArchiveRobotsPolicies.Captured;
                    """;
                command.Parameters.AddWithValue("$scheme",response.RequestUrl.Protocol);
                command.Parameters.AddWithValue("$host",response.RequestUrl.Hostname);
                command.Parameters.AddWithValue("$port",response.RequestUrl.Port);
                command.Parameters.AddWithValue("$captured",(response.RequestSent ?? DateTime.UtcNow).ToString("O"));
                command.Parameters.AddWithValue("$content",response.IsSuccess ? response.BodyText ?? "" : "");
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        int archived=0;
        foreach (var response in responses)
        {
            ct.ThrowIfCancellationRequested();
            if (_archiver.ArchiveResponse(response,isPublic:false)) archived++;
        }
        return archived;
    }

    public void Complete(CancellationToken ct)
    {
        var authorities=new List<(string scheme,string host,int port)>();
        using (var command=_connection.CreateCommand())
        {
            command.CommandText="SELECT Scheme,Host,Port FROM ArchiveWorkItems;";
            using var reader=command.ExecuteReader();
            while (reader.Read()) authorities.Add((reader.GetString(0),reader.GetString(1),reader.GetInt32(2)));
        }
        foreach (var authority in authorities)
        {
            ct.ThrowIfCancellationRequested();
            string? content;
            using (var command=_connection.CreateCommand())
            {
                command.CommandText="SELECT Content FROM ArchiveRobotsPolicies WHERE Scheme=$scheme AND Host=$host AND Port=$port;";
                AddAuthority(command,authority);
                content=command.ExecuteScalar() as string;
            }
            // Existing archives already contain historical robots captures.
            if (content==null)
            {
                var robotsUrl=new GeminiUrl(RobotsTxtParser.CreateRobotsUrl(authority.scheme,authority.host,authority.port));
                content=_archiver.GetLatestResponse(robotsUrl.ID)?.BodyText ?? "";
            }
            var robots=new RobotsTxtParser().Parse(content);
            using var db=_archiver.GetContext();
            long lastId=long.MinValue;
            while (true)
            {
                var urls=db.Urls.Where(u => u.Protocol==authority.scheme && u.Domain==authority.host && u.Port==authority.port && u.Id>lastId)
                    .OrderBy(u => u.Id).Take(500).ToList();
                if (urls.Count==0) break;
                foreach (var url in urls) url.IsPublic=robots.IsPathAllowed("archiver",url.GeminiUrl.Path);
                lastId=urls[^1].Id;
                db.SaveChanges();
                db.ChangeTracker.Clear();
                ct.ThrowIfCancellationRequested();
            }
            using var done=_connection.CreateCommand();
            done.CommandText="DELETE FROM ArchiveWorkItems WHERE Scheme=$scheme AND Host=$host AND Port=$port;";
            AddAuthority(done,authority);
            done.ExecuteNonQuery();
        }
        var stats=_archiver.GetArchiveStats();
        string packs=Path.Combine(_directory,"Packs");
        stats.PacksSizeOnDisk=Directory.Exists(packs) ? Directory.EnumerateFiles(packs,"*",SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0;
        string temporary=Path.Combine(_directory,"archive-stats.json.tmp");
        File.WriteAllText(temporary,JsonSerializer.Serialize(stats,new JsonSerializerOptions { WriteIndented=true }));
        File.Move(temporary,Path.Combine(_directory,"archive-stats.json"),overwrite:true);
        Console.WriteLine($"Archive: {stats.Captures:N0} captures, {stats.UrlsPublic:N0} public URLs.");
    }

    private static void AddAuthority(SqliteCommand command,(string scheme,string host,int port) authority)
    {
        command.Parameters.AddWithValue("$scheme",authority.scheme);
        command.Parameters.AddWithValue("$host",authority.host);
        command.Parameters.AddWithValue("$port",authority.port);
    }
    public void Dispose() => _connection.Dispose();
}
