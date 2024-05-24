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
    Stopwatch finalWatch;

    public SearchStorageWrapper(string storageDirectory)
    {
        webWatch = new Stopwatch();
        searchWatch = new Stopwatch();
        finalWatch = new Stopwatch();

        WebDB = new WebDatabase(storageDirectory);
        //searchDB has to be after WebDB, because the WebDB DB initialization creates the tables for the entities
        SearchDB = new SearchDatabase(storageDirectory);
    }

    public void FinalizeDatabases()
    {
        WebDB.FinalizeStores();

        finalWatch.Start();
        SearchDB.IndexFiles();
        PopularityCalculator popularityCalculator = new PopularityCalculator(WebDB.GetContext());
        popularityCalculator.Rank();
        finalWatch.Stop();

        System.Console.WriteLine($"WEB: {webWatch.Elapsed.TotalSeconds}");
        System.Console.WriteLine($"Search: {searchWatch.Elapsed.TotalSeconds}");
        System.Console.WriteLine($"Final: {finalWatch.Elapsed.TotalSeconds}");
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

            ret.LastUpdated = db.Documents.Select(x => x.LastVisit).Max();
        }

        return ret;
    }

    public bool StoreResponse(ParsedResponse response)
    {
        webWatch.Start();
        bool contentUpdated = WebDB.StoreResponse(response);
        webWatch.Stop();
        searchWatch.Start();
        SearchDB.UpdateIndex(response);
        searchWatch.Stop();
        return contentUpdated;
    }
}