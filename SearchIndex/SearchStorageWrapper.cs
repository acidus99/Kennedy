using System;
using System.Diagnostics;
using System.Linq;
using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Web;

namespace Kennedy.SearchIndex;

/// <summary>
/// Wraps the Search Database and Web Database
/// so you have a single interface to add/update documents and search indexes
/// </summary>
public class SearchStorageWrapper
{
    ISearchDatabase SearchDB;
    IWebDatabase WebDB;

    Stopwatch webWatch;
    Stopwatch searchWatch;
    Stopwatch globalWatch;

    public SearchStorageWrapper(string storageDirectory)
    {
        webWatch = new Stopwatch();
        searchWatch = new Stopwatch();
        globalWatch = new Stopwatch();

        WebDB = new WebDatabase(storageDirectory);
        //searchDB has to be after WebDB, because the WebDB DB initialization creates the tables for the entities
        SearchDB = new SearchDatabase(storageDirectory);
    }

    /// <summary>
    /// Called when we are done with all our StoreResponse() calls.
    /// Right now all this does is flush any pending bulk updates to the webdb (document/metadata database)
    /// </summary>
    public void FinalizeStores()
    {
        webWatch.Start();
        WebDB.FinalizeStores();
        webWatch.Stop();
    }

    /// <summary>
    /// Called when we want to do global-level analysis. This is usally after 1 or more WARC files have been added
    /// * Index Images
    /// * Indexing filepaths and link text
    /// * Computing and storing populat
    /// </summary>
    public void DoGlobalWork()
    {
        globalWatch.Start();
        SearchDB.IndexFiles();
        PopularityCalculator popularityCalculator = new PopularityCalculator(WebDB.GetContext());
        popularityCalculator.Rank();
        globalWatch.Stop();

        Console.WriteLine($"WebDB (doc meta data)\t{webWatch.Elapsed.TotalSeconds} sec");
        Console.WriteLine($"FTS index updates:\t{searchWatch.Elapsed.TotalSeconds} sec");
        Console.WriteLine($"Global:\t\t{globalWatch.Elapsed.TotalSeconds} sec");
    }

    public SearchStats GetSearchStats()
    {
        var ret = new SearchStats();

        using (var db = WebDB.GetContext())
        {
            ret.Domains = db.Documents
                .Where(x => x.StatusCode != GeminiParser.ConnectionErrorStatusCode)
                .Select(x => new { Domain = x.Domain, Port = x.Port })
                .Distinct()
                .LongCount();

            ret.Urls = db.Documents.LongCount();

            ret.SuccessUrls = db.Documents
                .Where(x => (x.StatusCode == 20))
                .LongCount();

            if (db.Documents.Any())
            {
                ret.LastUpdated = db.Documents.Select(x => x.LastVisit).Max();
            }
            else
            {
                ret.LastUpdated = DateTime.MinValue;
            }
        }

        return ret;
    }

    public void StoreResponse(ParsedResponse response)
    {
        webWatch.Start();
        FtsIndexAction action = WebDB.StoreResponse(response);
        webWatch.Stop();
        searchWatch.Start();
        SearchDB.UpdateIndex(action, response);
        searchWatch.Stop();
    }
}