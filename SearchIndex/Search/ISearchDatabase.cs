using System;
using System.Collections.Generic;

using Kennedy.Data;
using Kennedy.SearchIndex.Models;


namespace Kennedy.SearchIndex.Search
{
	public interface ISearchDatabase
	{
        /// <summary>
        /// Adds a document to the search index, keyed by its URL
        /// </summary>
        /// <param name="parsedResponse"></param>
        void UpdateIndex(ParsedResponse parsedResponse);

        /// <summary>
        /// Removes a document from the search index, based on its URL ID
        /// </summary>
        /// <param name="urlID"></param>
        void RemoveFromIndex(long urlID);

        /// <summary>
        /// Indexes images by using the link text of all inbound links. This has to be run
        /// at the end of a crawl so that all inbound link text is indexed
        /// </summary>
        void IndexImages();

        /// <summary>
        /// Gets the text that was used to index an image
        /// </summary>
        /// <param name="dbDocId"></param>
        /// <returns></returns>
        string GetImageIndexText(long urlID);

        /// <summary>
        /// Execute a text search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        List<FullTextSearchResult> DoTextSearch(string query, int offset, int limit);

        /// <summary>
        /// Returns the number of results for a text query
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        int GetTextResultsCount(string query);

        /// <summary>
        /// Executes an image search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        List<ImageSearchResult> DoImageSearch(string query, int offset, int limit);

        /// <summary>
        /// Returns the number of results for an image query
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        int GetImageResultsCount(string query);
    }
}

