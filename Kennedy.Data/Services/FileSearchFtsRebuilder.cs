using System.Text;
using System.Text.RegularExpressions;
using Kennedy.Data.Models;
using Kennedy.Data.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
            var linkText = await db.UrlLinks
                .Where(link => urlIds.Contains(link.TargetUrlId) && link.LinkText != "" &&
                    db.UrlRegistry.Any(source => source.Id==link.SourceUrlId && !source.IsRobotsExcluded &&
                        (source.Status==UrlStatus.Active || source.Status==UrlStatus.TemporaryError || source.Status==UrlStatus.ConnectionError)))
                .GroupBy(link => link.TargetUrlId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Select(link => link.LinkText).Distinct().ToList(),
                    ct);

            foreach (var workItem in workItems)
            {
                processed++;
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM FilesFts WHERE rowid = {workItem.UrlRegistryId};", ct);

                if (urls.TryGetValue(workItem.UrlRegistryId, out var url) &&
                    url.Status is UrlStatus.Active or UrlStatus.TemporaryError or UrlStatus.ConnectionError &&
                    url.LastMimeType != null && !UrlUtility.IsProactiveUrl(new Uri(url.NormalizedUrl)) &&
                    !textUrlIds.Contains(url.Id))
                {
                    var terms = BuildBaseTerms(url.NormalizedUrl);
                    if (linkText.TryGetValue(url.Id, out var incomingText))
                    {
                        terms = string.Join(' ', incomingText) + " " + terms;
                    }
                    terms = TokenCleaner.Replace(terms, " ").Trim().ToLowerInvariant();

                    if (terms.Length > 0)
                    {
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO FilesFts(rowid, UrlRegistryId, SearchText) VALUES ({url.Id}, {url.Id}, {terms});", ct);
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
            Console.WriteLine($"File-search index: processed {processed:N0} dirty URLs ({indexed:N0} refreshed)");
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
