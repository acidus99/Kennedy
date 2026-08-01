using System;
using System.Linq;
using Kennedy.SearchIndex.Models;

namespace Kennedy.SearchIndex.Web;

public class PopularityCalculator
{
    const int BatchSize = 500;

    readonly WebDatabaseContext db;

    public PopularityCalculator(WebDatabaseContext context)
    {
        db = context;
    }

    public void Rank()
    {
        int processed = 0;

        while (true)
        {
            var workItems = db.IndexWorkItems
                .Where(x => (x.WorkTypes & IndexWorkType.Popularity) != 0)
                .OrderBy(x => x.UrlID)
                .Take(BatchSize)
                .ToList();

            if (workItems.Count == 0)
            {
                break;
            }

            var urlIDs = workItems.Select(x => x.UrlID).ToList();
            var documents = db.Documents
                .Where(x => urlIDs.Contains(x.UrlID))
                .ToDictionary(x => x.UrlID);
            var inboundCounts = db.Links
                .Where(x => urlIDs.Contains(x.TargetUrlID) && x.IsExternal)
                .GroupBy(x => x.TargetUrlID)
                .Select(x => new { UrlID = x.Key, Count = x.Count() })
                .ToDictionary(x => x.UrlID, x => x.Count);

            foreach (var workItem in workItems)
            {
                processed++;
                if (documents.TryGetValue(workItem.UrlID, out var document))
                {
                    inboundCounts.TryGetValue(document.UrlID, out int inboundLinks);
                    document.ExternalInboundLinks = inboundLinks;
                    document.PopularityRank = document.IsAvailable ? CalculateRank(inboundLinks) : 0;
                }

                workItem.WorkTypes &= ~IndexWorkType.Popularity;
                if (workItem.WorkTypes == IndexWorkType.None)
                {
                    db.IndexWorkItems.Remove(workItem);
                }
            }

            db.SaveChanges();
            Console.WriteLine($"Popularity: recalculated {processed:N0} dirty URLs");
        }
    }

    private static double CalculateRank(int inboundLinks)
        => Math.Log(Math.Min(1 + inboundLinks, 100), 100);
}
