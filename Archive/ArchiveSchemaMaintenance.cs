using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Kennedy.Archive;

/// <summary>Archive index upgrades. Call before creating import temporary tables.</summary>
public static class ArchiveSchemaMaintenance
{
    public static void Initialize(SqliteConnection connection, Action<string>? progress = null)
    {
        var indexes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command=connection.CreateCommand())
        {
            command.CommandText="SELECT name FROM sqlite_master WHERE type='index'";
            using var reader=command.ExecuteReader();
            while (reader.Read()) indexes.Add(reader.GetString(0));
        }

        using var transaction=connection.BeginTransaction();
        void Execute(string sql)
        {
            using var command=connection.CreateCommand();
            command.Transaction=transaction; command.CommandText=sql; command.ExecuteNonQuery();
        }
        void Drop(string name)
        {
            if (indexes.Remove(name)) Execute($"DROP INDEX \"{name}\"");
        }
        void Ensure(string name,string table,string columns)
        {
            if (indexes.Contains(name)) return;
            progress?.Invoke($"Archive: creating {name} on {table}({columns})...");
            var watch=Stopwatch.StartNew();
            Execute($"CREATE INDEX \"{name}\" ON \"{table}\"({columns})");
            indexes.Add(name);
            progress?.Invoke($"Archive: {name} ready after {watch.Elapsed.TotalSeconds:F1}s.");
        }

        // Drop the larger composite first so its pages can be reused. DDL is
        // transactional: an interrupted/failed upgrade restores the former indexes.
        // The old hash-only index remains available until its replacement exists.
        Drop("IX_Snapshots_DataHash_UrlId_Captured");
        Ensure("IX_Snapshots_DataHash_UrlId","Snapshots","DataHash,UrlId");
        Drop("IX_Snapshots_DataHash");
        Ensure("IX_Snapshots_UrlId_Captured","Snapshots","UrlId,Captured");
        Ensure("IX_Urls_Protocol_Domain_Port","Urls","Protocol,Domain,Port");
        // Archive queries use the complete capsule key, primary key, or full scans.
        // The capsule index also covers the domain/port statistics projection.
        Drop("IX_Urls_Protocol");
        Drop("IX_Urls_Domain");
        Drop("IX_Urls_Port");
        Execute("DROP TABLE IF EXISTS WarcImports");
        transaction.Commit();
    }
}
