using System;
using System.Collections.Generic;
using System.Linq;
using Kennedy.Data;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Search;

public class FileIndexer
{
    const int BatchSize = 500;

    readonly string storageDirectory;
    readonly ISearchDatabase searchDatabase;
    readonly PathTokenizer pathTokenizer;

    public FileIndexer(string storageDirectory, ISearchDatabase searchDatabase)
    {
        this.storageDirectory = storageDirectory;
        this.searchDatabase = searchDatabase;
        pathTokenizer = new PathTokenizer();
    }

    public void IndexFiles()
    {
        int indexed = 0;
        int processed = 0;

        using (var db = GetContext())
        {
            while (true)
            {
                var workItems = db.IndexWorkItems
                    .Where(x => (x.WorkTypes & IndexWorkType.File) != 0)
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
                var linkText = db.Links
                    .Where(x => urlIDs.Contains(x.TargetUrlID) && x.LinkText != null && x.LinkText != "")
                    .ToList()
                    .GroupBy(x => x.TargetUrlID)
                    .ToDictionary(x => x.Key, x => x.Select(link => link.LinkText!).Distinct().ToList());

                foreach (var workItem in workItems)
                {
                    processed++;
                    if (documents.TryGetValue(workItem.UrlID, out var document) &&
                        !document.IsBodyIndexed &&
                        document.StatusCode == 20 &&
                        document.ContentType != ContentType.Image)
                    {
                        var terms = linkText.TryGetValue(document.UrlID, out var incomingText) ?
                            string.Join(' ', incomingText) + " " :
                            "";
                        terms += GetPathIndexText(document.GeminiUrl);
                        searchDatabase.RefreshIndexForUrl(document.UrlID, terms);
                        indexed++;
                    }
                    else if (!documents.TryGetValue(workItem.UrlID, out document) || !document.IsBodyIndexed)
                    {
                        searchDatabase.RemoveFileIndexEntry(workItem.UrlID);
                    }

                    CompleteWork(db, workItem, IndexWorkType.File);
                }

                db.SaveChanges();
                Console.WriteLine($"File index: processed {processed:N0} dirty URLs ({indexed:N0} refreshed)");
            }
        }
    }

    private static void CompleteWork(WebDatabaseContext db, IndexWorkItem workItem, IndexWorkType workType)
    {
        workItem.WorkTypes &= ~workType;
        if (workItem.WorkTypes == IndexWorkType.None)
        {
            db.IndexWorkItems.Remove(workItem);
        }
    }

    private string GetPathIndexText(Gemini.Net.GeminiUrl url)
    {
        string[] tokens = pathTokenizer.GetTokens(url);
        return tokens != null ? string.Join(' ', tokens) + " " : "";
    }

    private WebDatabaseContext GetContext()
        => new WebDatabaseContext(storageDirectory);
}
