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

    public SearchStorageWrapper(string storageDirectory)
    {
        WebDB = new WebDatabase(storageDirectory);
        //searchDB has to be after WebDB, because the WebDB DB initialization creates the tables for the entities
        SearchDB = new SearchDatabase(storageDirectory);
    }

    public void FinalizeDatabases()
    {
        SearchDB.IndexFiles();
        PopularityCalculator popularityCalculator = new PopularityCalculator(WebDB.GetContext());
        popularityCalculator.Rank();
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
        bool contentUpdated = WebDB.StoreResponse(response);
        SearchDB.UpdateIndex(response);
        return contentUpdated;
    }
}