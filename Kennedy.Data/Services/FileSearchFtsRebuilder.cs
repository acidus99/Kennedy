using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Services;

/// <summary>
/// Rebuilds FilesFts after WARC ingestion using link text grouped by target URL.
/// </summary>
public sealed class FileSearchFtsRebuilder
{
    private readonly IDbContextFactory<KennedyDbContext> _dbFactory;
    private static readonly Regex TokenCleaner = new(@"[\W_]+", RegexOptions.Compiled);

    public FileSearchFtsRebuilder(IDbContextFactory<KennedyDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task RebuildAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var textUrlIds = await db.Documents
            .Select(d => d.UrlRegistryId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .ToListAsync(ct);

        var textUrlSet = textUrlIds.ToHashSet();

        var candidates = await db.UrlRegistry
            .Where(u => u.LastStatusCode >= 20 && u.LastStatusCode < 30)
            .Select(u => new CandidateRow
            {
                Id = u.Id,
                NormalizedUrl = u.NormalizedUrl
            })
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
        candidates = candidates.Where(c => !textUrlSet.Contains(c.Id)).ToList();

        var candidateMap = candidates.ToDictionary(c => c.Id, c => c);
        var indexedTargets = new HashSet<long>();

        await db.Database.ExecuteSqlRawAsync("DELETE FROM FilesFts;", ct);

        await using var connection = (SqliteConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO FilesFts(rowid, UrlRegistryId, SearchText) VALUES ($rowid, $urlId, $text);";
        var rowIdParam = insertCommand.Parameters.Add("$rowid", SqliteType.Integer);
        var urlIdParam = insertCommand.Parameters.Add("$urlId", SqliteType.Integer);
        var textParam = insertCommand.Parameters.Add("$text", SqliteType.Text);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT links.TargetUrlId, links.LinkText
FROM UrlLinks AS links
JOIN UrlRegistry AS targets ON targets.Id = links.TargetUrlId
WHERE links.LinkText <> ''
  AND targets.LastStatusCode >= 20
  AND targets.LastStatusCode < 30
ORDER BY links.TargetUrlId ASC;";

        await using var reader = await command.ExecuteReaderAsync(ct);

        long currentTargetId = -1;
        var currentBuffer = new StringBuilder();

        while (await reader.ReadAsync(ct))
        {
            var targetId = reader.GetInt64(0);
            if (!candidateMap.ContainsKey(targetId))
            {
                continue;
            }

            var linkText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
            if (linkText.Length == 0)
            {
                continue;
            }

            if (currentTargetId != -1 && targetId != currentTargetId)
            {
                await WriteIndexRowAsync(currentTargetId, currentBuffer.ToString(), candidateMap, indexedTargets, insertCommand, rowIdParam, urlIdParam, textParam, ct);
                currentBuffer.Clear();
            }

            currentTargetId = targetId;
            if (currentBuffer.Length > 0)
            {
                currentBuffer.Append(' ');
            }
            currentBuffer.Append(linkText);
        }

        if (currentTargetId != -1)
        {
            await WriteIndexRowAsync(currentTargetId, currentBuffer.ToString(), candidateMap, indexedTargets, insertCommand, rowIdParam, urlIdParam, textParam, ct);
        }

        foreach (var candidate in candidates)
        {
            if (indexedTargets.Contains(candidate.Id))
            {
                continue;
            }

            var baseText = BuildBaseTerms(candidate.NormalizedUrl);
            if (baseText.Length == 0)
            {
                continue;
            }

            rowIdParam.Value = candidate.Id;
            urlIdParam.Value = candidate.Id;
            textParam.Value = baseText;
            await insertCommand.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task WriteIndexRowAsync(
        long targetId,
        string linkText,
        IReadOnlyDictionary<long, CandidateRow> candidateMap,
        ISet<long> indexedTargets,
        SqliteCommand insertCommand,
        SqliteParameter rowIdParam,
        SqliteParameter urlIdParam,
        SqliteParameter textParam,
        CancellationToken ct)
    {
        if (!candidateMap.TryGetValue(targetId, out var candidate))
        {
            return;
        }

        var baseText = BuildBaseTerms(candidate.NormalizedUrl);
        var merged = string.IsNullOrWhiteSpace(baseText) ? linkText : $"{baseText} {linkText}";
        merged = TokenCleaner.Replace(merged, " ").Trim().ToLowerInvariant();

        if (merged.Length == 0)
        {
            return;
        }

        rowIdParam.Value = targetId;
        urlIdParam.Value = targetId;
        textParam.Value = merged;
        await insertCommand.ExecuteNonQueryAsync(ct);
        indexedTargets.Add(targetId);
    }

    private static string BuildBaseTerms(string? normalizedUrl)
    {
        if (string.IsNullOrWhiteSpace(normalizedUrl) || !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var fileName = Path.GetFileName(uri.AbsolutePath);
        var pathAndQuery = string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
        if (!string.IsNullOrWhiteSpace(fileName)) builder.Append(fileName);
        if (builder.Length > 0) builder.Append(' ');
        builder.Append(pathAndQuery);

        return TokenCleaner.Replace(builder.ToString(), " ").Trim().ToLowerInvariant();
    }

    private sealed class CandidateRow
    {
        public long Id { get; init; }
        public string? NormalizedUrl { get; init; }
    }
}
