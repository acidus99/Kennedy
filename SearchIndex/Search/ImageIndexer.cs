using System;
using System.Linq;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Search;

internal class ImageIndexer
{
    const int BatchSize = 500;

    readonly string storageDirectory;
    readonly ISearchDatabase searchDatabase;
    readonly PathTokenizer pathTokenizer = new PathTokenizer();

    public ImageIndexer(string storageDirectory, ISearchDatabase searchDatabase)
    {
        this.storageDirectory = storageDirectory;
        this.searchDatabase = searchDatabase;
    }

    public void IndexImages()
    {
        int indexed = 0;
        int processed = 0;

        using (var db = GetContext())
        {
            while (true)
            {
                var workItems = db.IndexWorkItems
                    .Where(x => (x.WorkTypes & IndexWorkType.Image) != 0)
                    .OrderBy(x => x.UrlID)
                    .Take(BatchSize)
                    .ToList();

                if (workItems.Count == 0)
                {
                    break;
                }

                var urlIDs = workItems.Select(x => x.UrlID).ToList();
                var documents = db.Documents.Include(x => x.Image)
                    .Where(x => urlIDs.Contains(x.UrlID))
                    .ToDictionary(x => x.UrlID);
                var linkText = db.Links
                    .Where(x => urlIDs.Contains(x.TargetUrlID) && x.LinkText != null && x.LinkText != "")
                    .ToList()
                    .GroupBy(x => x.TargetUrlID)
                    .ToDictionary(x => x.Key, x => x.Select(link => link.LinkText!).Distinct().ToList());

                foreach (var workItem in workItems)
                {
                    processed++;
                    if (documents.TryGetValue(workItem.UrlID, out var document) && document.Image != null &&
                        linkText.TryGetValue(workItem.UrlID, out var incomingText))
                    {
                        var terms = string.Join(' ', incomingText) + " " + GetPathIndexText(document.Url);
                        searchDatabase.RefreshImageIndexForUrl(document.UrlID, terms);
                        indexed++;
                    }
                    else
                    {
                        searchDatabase.RemoveImageIndexEntry(workItem.UrlID);
                    }

                    CompleteWork(db, workItem);
                }

                db.SaveChanges();
                Console.WriteLine($"Image index: processed {processed:N0} dirty URLs ({indexed:N0} refreshed)");
            }
        }
    }

    private static void CompleteWork(WebDatabaseContext db, IndexWorkItem workItem)
    {
        workItem.WorkTypes &= ~IndexWorkType.Image;
        if (workItem.WorkTypes == IndexWorkType.None)
        {
            db.IndexWorkItems.Remove(workItem);
        }
    }

    private string GetPathIndexText(string url)
    {
        string[] tokens = pathTokenizer.GetTokens(url);
        return tokens != null ? string.Join(' ', tokens) + " " : "";
    }

    private WebDatabaseContext GetContext()
        => new WebDatabaseContext(storageDirectory);
}
