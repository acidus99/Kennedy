using Kennedy.Search.Models;

namespace Kennedy.Search.Services;

/// <summary>
/// Abstraction over the full-text and image search capabilities of the Kennedy search database.
/// The primary implementation is <see cref="SqliteSearchService"/>, which issues raw SQL against
/// the FTS5 virtual tables.
/// </summary>
public interface ISearchService
{
    /// <summary>Returns the total number of text documents matching <paramref name="query"/> (for pagination).</summary>
    int GetTextResultsCount(UserQuery query);

    /// <summary>
    /// Returns a page of text search results for <paramref name="query"/>.
    /// Results are ordered by <c>LastIndexedUtc DESC</c>.
    /// </summary>
    IReadOnlyList<TextSearchResult> SearchText(UserQuery query, int offset, int limit);

    /// <summary>Returns the total number of image URLs matching <paramref name="query"/> (for pagination).</summary>
    int GetImageResultsCount(UserQuery query);

    /// <summary>
    /// Returns a page of image search results for <paramref name="query"/>.
    /// Results are ordered by <c>LastVisit DESC</c>.
    /// </summary>
    IReadOnlyList<ImageSearchResult> SearchImages(UserQuery query, int offset, int limit);
}
