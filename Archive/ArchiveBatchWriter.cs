using System.Buffers.Binary;
using Gemini.Net;
using Kennedy.Archive.Pack;
using Microsoft.Data.Sqlite;

namespace Kennedy.Archive;

/// <summary>A single archive writer with prepared SQL and per-append pack streams.</summary>
public sealed class ArchiveBatchWriter : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PackManager _packs;
    private readonly Dictionary<string, SqliteCommand> _commands = new();
    public long AlreadyArchived { get; private set; }

    public ArchiveBatchWriter(SqliteConnection connection, string packsDirectory)
    {
        _connection = connection;
        _packs = new PackManager(packsDirectory);
        using var setup = connection.CreateCommand();
        setup.CommandText = """
            CREATE TEMP TABLE IF NOT EXISTS ImportCaptureKeys(
                UrlId INTEGER, Captured TEXT, PRIMARY KEY(UrlId,Captured)) WITHOUT ROWID;
            """;
        setup.ExecuteNonQuery();
    }

    public int StoreBatch(IReadOnlyList<GeminiResponse> responses, SqliteTransaction transaction, CancellationToken ct)
    {
        var candidates = responses.Where(Archiver.ShouldBeArchived).ToList();
        Command("DELETE FROM temp.ImportCaptureKeys", transaction).ExecuteNonQuery();
        foreach (var response in candidates)
            Command("INSERT OR IGNORE INTO temp.ImportCaptureKeys VALUES($url,$captured)", transaction,
                ("$url",response.RequestUrl.ID),("$captured",Captured(response))).ExecuteNonQuery();

        // Anchor the small batch, then probe the capture index. Never scan Snapshots.
        var seen = new HashSet<(long, DateTime)>();
        using (var reader = Command("""
            SELECT k.UrlId,k.Captured FROM temp.ImportCaptureKeys k
            CROSS JOIN Snapshots s INDEXED BY IX_Snapshots_UrlId_Captured
            ON s.UrlId=k.UrlId AND s.Captured=k.Captured
            """,transaction).ExecuteReader())
            while (reader.Read()) seen.Add((reader.GetInt64(0),reader.GetDateTime(1)));

        int added = 0;
        var offsets = new Dictionary<string, long>();
        foreach (var original in candidates)
        {
            ct.ThrowIfCancellationRequested();
            long urlId = original.RequestUrl.ID;
            DateTime captured = Captured(original);
            if (!seen.Add((urlId,captured))) { AlreadyArchived++; continue; }

            var response = Archiver.CopyForArchive(original);
            var bytes = GeminiParser.CreateResponseBytes(response);
            string hash = GeminiParser.GetStrongHash(bytes);
            Command("""
                INSERT OR IGNORE INTO Urls(Id,FullUrl,Domain,Port,Protocol,IsPublic)
                VALUES($id,$url,$host,$port,$scheme,0)
                """,transaction,("$id",urlId),("$url",response.RequestUrl.NormalizedUrl),
                ("$host",response.RequestUrl.Hostname),("$port",response.RequestUrl.Port),
                ("$scheme",response.RequestUrl.Protocol)).ExecuteNonQuery();

            bool duplicate = false, globalDuplicate = false;
            long offset;
            if (!offsets.TryGetValue(hash,out offset))
            {
                var previous = Command("SELECT Offset FROM Snapshots WHERE DataHash=$hash LIMIT 1",transaction,("$hash",hash)).ExecuteScalar();
                offset = previous == null ? Append(hash,bytes) : Convert.ToInt64(previous);
                offsets[hash] = offset;
            }
            long? earlierId = null;
            using (var reader = Command("""
                SELECT Id,Captured,IsGlobalDuplicate FROM Snapshots
                WHERE DataHash=$hash AND UrlId=$url ORDER BY Captured LIMIT 1
                """,transaction,("$hash",hash),("$url",urlId)).ExecuteReader())
            {
                if (reader.Read())
                {
                    duplicate = reader.GetDateTime(1) <= captured;
                    if (!duplicate) { earlierId=reader.GetInt64(0); globalDuplicate=reader.GetBoolean(2); }
                }
            }
            if (earlierId.HasValue)
                Command("UPDATE Snapshots SET IsDuplicate=1 WHERE Id=$id",transaction,("$id",earlierId.Value)).ExecuteNonQuery();
            else
                globalDuplicate = Command("SELECT 1 FROM Snapshots WHERE DataHash=$hash AND UrlId!=$url LIMIT 1",
                    transaction,("$hash",hash),("$url",urlId)).ExecuteScalar()!=null;

            Command("""
                INSERT INTO Snapshots(Offset,Size,IsDuplicate,IsGlobalDuplicate,StatusCode,DataHash,
                    Mimetype,Captured,UrlId,HasBodyContent,IsBodyTruncated)
                VALUES($offset,$size,$duplicate,$global,$status,$hash,$mime,$captured,$url,$body,$truncated)
                """,transaction,("$offset",offset),("$size",bytes.LongLength),("$duplicate",duplicate),
                ("$global",globalDuplicate),("$status",response.StatusCode),("$hash",hash),
                ("$mime",response.MimeType),("$captured",captured),("$url",urlId),
                ("$body",response.HasBody),("$truncated",response.IsBodyTruncated)).ExecuteNonQuery();
            added++;
        }
        return added;
    }

    private long Append(string hash, byte[] bytes)
    {
        var record = PackRecordFactory.MakeOptimalRecord(bytes);
        // Hash prefixes have negligible locality. Close each append to flush its
        // bytes before the metadata transaction commits. A failed transaction may
        // leave unreferenced bytes, but replay never references unwritten data.
        using var stream = _packs.GetPack(hash).OpenAppender();
        long offset = stream.Position;
        Span<byte> header = stackalloc byte[8];
        System.Text.Encoding.ASCII.GetBytes(record.Type.PadRight(4),header[..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..],(uint)record.Data.Length);
        stream.Write(header); stream.Write(record.Data);
        return offset;
    }

    private SqliteCommand Command(string sql, SqliteTransaction transaction, params (string name, object? value)[] parameters)
    {
        if (!_commands.TryGetValue(sql,out var command))
        {
            command = _connection.CreateCommand(); command.CommandText=sql;
            foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.name,parameter.value ?? DBNull.Value);
            command.Prepare(); _commands.Add(sql,command);
        }
        command.Transaction=transaction;
        foreach (var parameter in parameters) command.Parameters[parameter.name].Value=parameter.value ?? DBNull.Value;
        return command;
    }

    private static DateTime Captured(GeminiResponse response) => response.ResponseReceived ?? response.RequestSent ?? DateTime.UtcNow;

    public void Dispose()
    {
        foreach (var command in _commands.Values) command.Dispose();
    }
}
