using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kennedy.Data.Services;

/// <summary>Idempotent initialization and repair of Kennedy2 databases only.</summary>
public static class DatabaseMaintenance
{
    public static async Task InitializeAsync(KennedyDbContext db, CancellationToken ct = default)
    {
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) tables.Add(reader.GetString(0));
        }
        if (tables.Count > 0 && !tables.Contains("UrlRegistry"))
            throw new InvalidOperationException("This is not a Kennedy2 search database. Select a kennedy2.db file; legacy main databases are not upgraded.");

        await db.ApplyPerformancePragmasAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        string script = db.Database.GenerateCreateScript();
        foreach (Match statement in Regex.Matches(script, @"CREATE TABLE .*?;", RegexOptions.Singleline))
            await db.Database.ExecuteSqlRawAsync(statement.Value.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS "), ct);

        bool initializeWatermarks = false;
        bool initializePolicies = false;
        foreach (var table in db.Model.GetRelationalModel().Tables)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = $"PRAGMA table_info({Quote(table.Name)});";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(1));
            }
            foreach (var column in table.Columns.Where(c => !columns.Contains(c.Name)))
            {
                if (table.PrimaryKey?.Columns.Contains(column) == true)
                    throw new InvalidOperationException($"Cannot repair missing primary key {table.Name}.{column.Name}.");
                string defaultValue = column.StoreType == "TEXT" ? "''" : "0";
                if (column.PropertyMappings.Any(m => m.Property.ClrType == typeof(DateTime))) defaultValue = "'0001-01-01 00:00:00'";
                string definition = column.IsNullable ? "" : $" NOT NULL DEFAULT {defaultValue}";
                // Identifiers and store types come from the EF model, never user input.
                string alter = $"ALTER TABLE {Quote(table.Name)} ADD COLUMN {Quote(column.Name)} {column.StoreType}{definition};";
                await db.Database.ExecuteSqlRawAsync(alter, ct);
                initializeWatermarks |= table.Name == "UrlRegistry" && column.Name == "LastIndexedVisit";
                initializePolicies |= table.Name == "UrlRegistry" && column.Name == "IsRobotsExcluded";
            }
        }
        await using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction.GetDbTransaction();
            check.CommandText = "SELECT 1 FROM sqlite_master WHERE type='index' AND name='UX_Documents_UrlRegistryId';";
            if (await check.ExecuteScalarAsync(ct) == null)
                await db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM Documents WHERE Id IN (
                        SELECT Id FROM (SELECT Id,ROW_NUMBER() OVER (
                            PARTITION BY UrlRegistryId ORDER BY LastIndexedUtc DESC,Id DESC) AS Position
                            FROM Documents WHERE UrlRegistryId IS NOT NULL) WHERE Position>1);
                    """, ct);
        }
        foreach (Match statement in Regex.Matches(script, @"CREATE (?:UNIQUE )?INDEX .*?;", RegexOptions.Singleline))
            await db.Database.ExecuteSqlRawAsync(Regex.Replace(statement.Value, @"CREATE (UNIQUE )?INDEX ", "CREATE $1INDEX IF NOT EXISTS "), ct);

        if (initializeWatermarks)
        {
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE UrlRegistry SET
                    LastIndexedVisit = CASE WHEN Status IN (2,3) THEN
                        COALESCE((SELECT LastIndexedUtc FROM Documents WHERE UrlRegistryId=UrlRegistry.Id),
                                 (SELECT LastIndexedUtc FROM Images WHERE UrlRegistryId=UrlRegistry.Id))
                        ELSE MAX(COALESCE(LastVisit,'0001-01-01 00:00:00'),
                            COALESCE((SELECT LastIndexedUtc FROM Documents WHERE UrlRegistryId=UrlRegistry.Id),'0001-01-01 00:00:00'),
                            COALESCE((SELECT LastIndexedUtc FROM Images WHERE UrlRegistryId=UrlRegistry.Id),'0001-01-01 00:00:00')) END,
                    LastIndexedHash = COALESCE((SELECT ResponseHash FROM Documents WHERE UrlRegistryId=UrlRegistry.Id), LastContentHash),
                    BodySize = COALESCE((SELECT BodySize FROM Documents WHERE UrlRegistryId=UrlRegistry.Id),0),
                    IsBodyTruncated = COALESCE((SELECT IsBodyTruncated FROM Documents WHERE UrlRegistryId=UrlRegistry.Id),0);
                """, ct);
        }
        await transaction.CommitAsync(ct);
        if (initializePolicies)
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO IndexWorkItems(UrlRegistryId,WorkTypes)
                SELECT SourceUrlRegistryId,2 FROM RobotsTxts WHERE 1
                ON CONFLICT(UrlRegistryId) DO UPDATE SET WorkTypes=WorkTypes|2;
                """, ct);
        await db.EnsureFtsAsync(ct);
        // Remove orphaned derived rows and queue missing file entries. Original text
        // bodies live in WARCs; replaying those also repairs missing DocumentsFts rows.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM DocumentsFts WHERE rowid NOT IN (SELECT Id FROM Documents);", ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM FilesFts WHERE rowid NOT IN (SELECT Id FROM UrlRegistry);", ct);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO IndexWorkItems(UrlRegistryId,WorkTypes)
            SELECT u.Id,1 FROM UrlRegistry u
            WHERE u.Status IN (1,2,3) AND u.LastMimeType IS NOT NULL
              AND u.NormalizedUrl NOT LIKE '%/robots.txt' AND u.NormalizedUrl NOT LIKE '%/favicon.txt'
              AND u.NormalizedUrl NOT LIKE '%/.well-known/security.txt'
              AND NOT EXISTS(SELECT 1 FROM Documents d WHERE d.UrlRegistryId=u.Id)
              AND NOT EXISTS(SELECT 1 FROM FilesFts f WHERE f.rowid=u.Id)
            ON CONFLICT(UrlRegistryId) DO UPDATE SET WorkTypes=WorkTypes|1;
            """, ct);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
