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
        await db.Database.OpenConnectionAsync(ct);
        await db.ApplyPerformancePragmasAsync(ct);
        var progress=System.Diagnostics.Stopwatch.StartNew();
        int processed=0;
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
                if (!robots.HasValidRules)
                {
                    var changed = await db.UrlRegistry.Where(u => u.Scheme==source.Scheme && u.Host==source.Host &&
                        u.Port==source.Port && u.IsRobotsExcluded).Select(u => u.Id).ToListAsync(ct);
                    foreach (var batch in changed.Chunk(900))
                    {
                        var targets=await db.UrlLinks.Where(l=>batch.Contains(l.SourceUrlId)).Select(l=>l.TargetUrlId).Distinct().ToListAsync(ct);
                        await ResponseStore.QueueFileIndexWorkAsync(db,targets,ct);
                    }
                    await db.UrlRegistry.Where(u => u.Scheme==source.Scheme && u.Host==source.Host &&
                        u.Port==source.Port && u.IsRobotsExcluded).ExecuteUpdateAsync(set => set.SetProperty(u=>u.IsRobotsExcluded,false),ct);
                }
                else
                {
                    long lastId = 0;
                    while (true)
                    {
                        var urls = await db.UrlRegistry.Where(u => u.Scheme==source.Scheme && u.Host==source.Host && u.Port==source.Port && u.Id>lastId)
                            .OrderBy(u => u.Id).Take(500).ToListAsync(ct);
                        if (urls.Count==0) break;
                        var changed=new List<long>();
                        foreach (var url in urls)
                        {
                            bool excluded=!robots.IsPathAllowed("indexer",new GeminiUrl(url.NormalizedUrl).Path);
                            if(url.IsRobotsExcluded!=excluded) changed.Add(url.Id);
                            url.IsRobotsExcluded=excluded;
                        }
                        if(changed.Count>0)
                        {
                            var targets=await db.UrlLinks.Where(l=>changed.Contains(l.SourceUrlId)).Select(l=>l.TargetUrlId).Distinct().ToListAsync(ct);
                            await ResponseStore.QueueFileIndexWorkAsync(db,targets,ct);
                        }
                        lastId=urls[^1].Id;
                        await db.SaveChangesAsync(ct);
                        // Keep only the work item and source alive across large capsules.
                        foreach (var url in urls) if (url.Id!=source.Id) db.Entry(url).State=EntityState.Detached;
                    }
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
            processed++;
            if (progress.Elapsed.TotalSeconds>=5)
            {
                Console.WriteLine($"Search robots policy: {processed:N0}/{ids.Count:N0} capsules.");
                progress.Restart();
            }
        }
        Console.WriteLine($"Search robots policy: {processed:N0} capsules completed.");
    }
}
