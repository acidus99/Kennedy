using System.Collections.Generic;
using Kennedy.Data;
using Kennedy.SearchIndex.Models;

namespace Kennedy.SearchIndex.Search;

public interface ISearchDatabase
{
    /// <summary>
    /// Updates the search index, if appropriate
    /// </summary>
    /// <param name="parsedResponse"></param>
    void UpdateIndex(ParsedResponse parsedResponse);

    /// <summary>
    /// Updates the search index for a specific URL
    /// </summary>
    /// <param name="urlID"></param>
    /// <param name="filteredBody"></param>
    /// <param name="title"></param>
    void UpdateIndexForUrl(long urlID, string filteredBody, string? title = null);

    /// <summary>
    /// Removes a document from the search index, based on its URL ID
    /// </summary>
    /// <param name="urlID"></param>
    void RemoveFromIndex(long urlID);

    /// <summary>
    /// Indexes images by using the link text of all inbound links. This has to be run
    /// at the end of a crawl so that all inbound link text is indexed
    /// </summary>
    void IndexFiles();

    /// <summary>
    /// Gets the text that was used to index an image
    /// </summary>
    /// <param name="dbDocId"></param>
    /// <returns></returns>
    string? GetImageIndexText(long urlID);

    /// <summary>
    /// Execute a text search
    /// </summary>
    /// <param name="query"></param>
    /// <param name="offset"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    List<FullTextSearchResult> DoTextSearch(UserQuery query, int offset, int limit);

    /// <summary>
    /// Returns the number of results for a text query
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    int GetTextResultsCount(UserQuery query);

    /// <summary>
    /// Executes an image search
    /// </summary>
    /// <param name="query"></param>
    /// <param name="offset"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    List<ImageSearchResult> DoImageSearch(UserQuery query, int offset, int limit);

    /// <summary>
    /// Returns the number of results for an image query
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    int GetImageResultsCount(UserQuery query);
}