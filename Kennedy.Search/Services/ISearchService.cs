using Kennedy.Search.Models;

namespace Kennedy.Search.Services;

public interface ISearchService
{
    int GetTextResultsCount(UserQuery query);
    IReadOnlyList<TextSearchResult> SearchText(UserQuery query, int offset, int limit);

    int GetImageResultsCount(UserQuery query);
    IReadOnlyList<ImageSearchResult> SearchImages(UserQuery query, int offset, int limit);
}
