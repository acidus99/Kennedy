using Kennedy.Data;

namespace Kennedy.SearchIndex.Web;

public interface IWebDatabase
{
    WebDatabaseContext GetContext();

    /// <summary>
    /// Stores/updates a response in our web database.
    /// </summary>
    /// <param name="parsedResponse"></param>
    /// <returns>true if the response's content changed</returns>
    FtsIndexAction StoreResponse(ParsedResponse parsedResponse);

    void FinalizeStores();
}

public enum FtsIndexAction
{
    Nothing,
    DeletePrevious,
    RefreshWithCurrent,
    AddCurrent
}