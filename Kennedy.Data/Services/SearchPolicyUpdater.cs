using Gemini.Net;
using Kennedy.Data.Models;
using Kennedy.Data.RobotsTxt;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Services;

/// <summary>Apply robots rules without destroying the retained searchable payload.</summary>
public sealed class SearchPolicyUpdater(IDbContextFactory<KennedyDbContext> factory)
{
    public async Task ApplyAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var ids = await db.IndexWorkItems.Where(w => (w.WorkTypes & IndexWorkType.RobotsPolicy) != 0)
            .Select(w => w.UrlRegistryId).ToListAsync(ct);
        foreach (var id in ids)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var source = await db.UrlRegistry.FindAsync([id], ct);
            if (source != null)
            {
                var content = await db.RobotsTxts.Where(r => r.Scheme==source.Scheme && r.Host==source.Host && r.Port==source.Port)
                    .Select(r => r.Content).FirstOrDefaultAsync(ct);
                var robots = new RobotsTxtParser().Parse(content ?? "");
                long lastId = 0;
                while (true)
                {
                    var urls = await db.UrlRegistry.Where(u => u.Scheme==source.Scheme && u.Host==source.Host && u.Port==source.Port && u.Id>lastId)
                        .OrderBy(u => u.Id).Take(500).ToListAsync(ct);
                    if (urls.Count==0) break;
                    foreach (var url in urls)
                        url.IsRobotsExcluded = !robots.IsPathAllowed("indexer", new GeminiUrl(url.NormalizedUrl).Path);
                    lastId=urls[^1].Id;
                    await db.SaveChangesAsync(ct);
                    // Keep only the work item and source alive across large capsules.
                    foreach (var url in urls) if (url.Id!=source.Id) db.Entry(url).State=EntityState.Detached;
                }
            }
            var work = await db.IndexWorkItems.FindAsync([id], ct);
            if (work != null)
            {
                work.WorkTypes &= ~IndexWorkType.RobotsPolicy;
                if (work.WorkTypes==IndexWorkType.None) db.IndexWorkItems.Remove(work);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }
    }
}
