using System.Text;
using System.Text.RegularExpressions;
using Kennedy.Data.Models;
using Kennedy.Data.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kennedy.Data.Services;

/// <summary>
/// Refreshes only dirty FilesFts rows after WARC ingestion.
/// </summary>
public sealed class FileSearchFtsRebuilder
{
    private const int BatchSize = 500;
    private readonly IDbContextFactory<KennedyDbContext> _dbFactory;
    private static readonly Regex TokenCleaner = new(@"[\W_]+", RegexOptions.Compiled);

    public FileSearchFtsRebuilder(IDbContextFactory<KennedyDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task RebuildAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        await db.ApplyPerformancePragmasAsync(ct);
        var progress = System.Diagnostics.Stopwatch.StartNew();
        int processed = 0;
        int indexed = 0;

        while (true)
        {
            var workItems = await db.IndexWorkItems
                .Where(item => (item.WorkTypes & IndexWorkType.FileSearch) != 0)
                .OrderBy(item => item.UrlRegistryId)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (workItems.Count == 0)
            {
                Console.WriteLine($"File-search index: processed {processed:N0} dirty URLs ({indexed:N0} refreshed)");
                return;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var urlIds = workItems.Select(item => item.UrlRegistryId).ToList();
            var urls = await db.UrlRegistry
                .Where(url => urlIds.Contains(url.Id))
                .ToDictionaryAsync(url => url.Id, ct);
            var textUrlIds = await db.Documents
                .Where(document => document.UrlRegistryId != null && urlIds.Contains(document.UrlRegistryId.Value))
                .Select(document => document.UrlRegistryId!.Value)
                .ToHashSetAsync(ct);
            // Text pages never enter FilesFts; don't gather their incoming link labels.
            var eligible = urls.Values.Where(url =>
                    url.Status is UrlStatus.Active or UrlStatus.TemporaryError or UrlStatus.ConnectionError &&
                    url.LastMimeType != null && !UrlUtility.IsProactiveUrl(new Uri(url.NormalizedUrl)) &&
                    !textUrlIds.Contains(url.Id)).ToDictionary(url => url.Id);
            var fileIds = eligible.Keys.ToArray();
            var linkText = await db.UrlLinks
                .Where(link => fileIds.Contains(link.TargetUrlId) && link.LinkText != "" &&
                    db.UrlRegistry.Any(source => source.Id==link.SourceUrlId && !source.IsRobotsExcluded &&
                        (source.Status==UrlStatus.Active || source.Status==UrlStatus.TemporaryError || source.Status==UrlStatus.ConnectionError)))
                .GroupBy(link => link.TargetUrlId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Select(link => link.LinkText).Distinct().ToList(),
                    ct);

            await using var delete = db.Database.GetDbConnection().CreateCommand();
            await using var insert = db.Database.GetDbConnection().CreateCommand();
            delete.Transaction = insert.Transaction = transaction.GetDbTransaction();
            delete.CommandText="DELETE FROM FilesFts WHERE rowid=$id";
            var deleteId=delete.CreateParameter(); deleteId.ParameterName="$id"; deleteId.Value=0L; delete.Parameters.Add(deleteId);
            insert.CommandText="INSERT INTO FilesFts(rowid,UrlRegistryId,SearchText) VALUES($id,$id,$text)";
            var insertId=insert.CreateParameter(); insertId.ParameterName="$id"; insertId.Value=0L; insert.Parameters.Add(insertId);
            var insertText=insert.CreateParameter(); insertText.ParameterName="$text"; insertText.Value=""; insert.Parameters.Add(insertText);
            delete.Prepare(); insert.Prepare();
            foreach (var workItem in workItems)
            {
                processed++;
                deleteId.Value=workItem.UrlRegistryId;
                await delete.ExecuteNonQueryAsync(ct);

                if (eligible.TryGetValue(workItem.UrlRegistryId, out var url))
                {
                    var terms = BuildBaseTerms(url.NormalizedUrl);
                    if (linkText.TryGetValue(url.Id, out var incomingText))
                    {
                        terms = string.Join(' ', incomingText) + " " + terms;
                    }
                    terms = TokenCleaner.Replace(terms, " ").Trim().ToLowerInvariant();

                    if (terms.Length > 0)
                    {
                        insertId.Value=url.Id; insertText.Value=terms;
                        await insert.ExecuteNonQueryAsync(ct);
                        indexed++;
                    }
                }

                workItem.WorkTypes &= ~IndexWorkType.FileSearch;
                if (workItem.WorkTypes == IndexWorkType.None)
                {
                    db.IndexWorkItems.Remove(workItem);
                }
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();
            if (progress.Elapsed.TotalSeconds >= 5)
            {
                Console.WriteLine($"File-search index: processed {processed:N0} dirty URLs ({indexed:N0} refreshed)");
                progress.Restart();
            }
        }
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
}
