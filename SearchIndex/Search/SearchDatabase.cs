using System;
using System.Linq;
using System.Collections.Generic;

using System.Data.SqlTypes;
using Microsoft.Data.Sqlite;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Models;
using Microsoft.EntityFrameworkCore;
using static System.Net.WebRequestMethods;

namespace Kennedy.SearchIndex.Search
{
	public class SearchDatabase : ISearchDatabase
	{
        string connectionString;
        string storageDirectory;


        public SearchDatabase(string storageDirectory)
        {
            this.storageDirectory = storageDirectory;
            connectionString = $"Data Source='{storageDirectory}doc-index.db'";
            EnsureFullTextSearch();
        }

        #region Add to Index

        public void UpdateIndex(ParsedResponse parsedResponse)
        {
            var gemText = parsedResponse as GemTextResponse;

            //for now, only gemtext is indexed
            if (gemText != null && gemText.IsIndexable)
            {
                UpdateTextIndex(parsedResponse.RequestUrl.ID, gemText.Title, gemText.FilteredBody);
            }
        }

        private void UpdateTextIndex(long dbDocID, string? title, string filteredBody)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    //first delete all FTS entries for this
                    SqliteCommand cmd = new SqliteCommand(@"DELETE From FTS as fts WHERE fts.ROWID = $docid", connection, transaction);
                    cmd.Parameters.Add(new SqliteParameter("$docid", dbDocID));

                    cmd.ExecuteNonQuery();

                    cmd = new SqliteCommand(@"INSERT INTO FTS(ROWID, Title, Body) VALUES ($docid, $title, $body)", connection, transaction); ;
                    cmd.Parameters.Add(new SqliteParameter("$docid", dbDocID));

                    if (title == null)
                    {
                        cmd.Parameters.Add(new SqliteParameter("$title", DBNull.Value));
                    } else
                    {
                        cmd.Parameters.Add(new SqliteParameter("$title", title));
                    }
                    cmd.Parameters.Add(new SqliteParameter("$body", filteredBody));
                    cmd.ExecuteNonQuery();

                    transaction.Commit();
                }
                connection.Close();
            }
        }

        #endregion

        #region Index Images

        public void IndexImages()
        {
            ImageIndexer imageIndexer = new ImageIndexer(connectionString);
            imageIndexer.IndexImages();
        }

        #endregion

        #region Image Search

        public string GetImageIndexText(long urlID)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var cmd = new SqliteCommand(@"SELECT Terms FROM ImageSearch WHERE ROWID = $rowid", connection);
                cmd.Parameters.Add(new SqliteParameter("$rowid", urlID));
                return (cmd.ExecuteScalar() as string) ?? "";
            }
        }

        public int GetImageResultsCount(string query)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    SqliteCommand cmd = new SqliteCommand(@"Select count(*) From ImageSearch WHERE Terms match $query", connection);
                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception)
            {
            }
            return 0;
        }

        private FormattableString GetImageSearchQuery(string query, int offset, int limit)
        {
            var queryParam = new SqliteParameter("query", query);
            var offsetParam = new SqliteParameter("offset", offset);
            var limitParam = new SqliteParameter("limit", limit);

            return $@"
Select img.UrlID, url, BodySize, Width, Height, ImageType, snippet(ImageSearch, 0, '[',']','…',20) as Snippet, ( rank + (rank*0.3*PopularityRank)) as tot
From ImageSearch as fts
 Inner Join Images as img
On img.UrlID = fts.ROWID
 Inner join Documents as doc
On doc.UrlID = img.UrlID
WHERE Terms match {queryParam}
order by tot
LIMIT {limit} OFFSET {offset}";
        }

        public List<ImageSearchResult> DoImageSearch(string query, int offset, int limit)
        {
            using (var db = new Kennedy.SearchIndex.Web.WebDatabaseContext(storageDirectory))
            {
                var sql = GetImageSearchQuery(query, offset, limit);
                var results = db.ImageResults.FromSql(sql);
                return results.ToList();
            }
        }

        #endregion

        #region Text search

        public int GetTextResultsCount(string query)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    SqliteCommand cmd = new SqliteCommand(@"Select count(*) From FTS WHERE Body match $query", connection);
                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception)
            {
            }
            return 0;
        }

        public List<FullTextSearchResult> DoTextSearch(string query, int offset, int limit)
        {
            using (var db = new Kennedy.SearchIndex.Web.WebDatabaseContext(storageDirectory))
            {
                var sql = GetTextSearchQuery(query, offset, limit);
                var results = db.FtsResults.FromSql(sql);
                return results.ToList();
            }
        }

        private FormattableString GetTextSearchQuery(string query, int offset, int limit)
        {
            var queryParam = new SqliteParameter("query", query);
            var offsetParam = new SqliteParameter("offset", offset);
            var limitParam = new SqliteParameter("limit", limit);

            return
$@"Select Url, BodySize, doc.Title, UrlID, DetectedLanguage, LineCount, MimeType, ( rank + (rank*0.3*PopularityRank)) as TotalRank, snippet(FTS, 1, '[',']','…',20) as Snippet
From FTS as fts
Inner Join Documents as doc
On doc.UrlID = fts.ROWID
WHERE Body MATCH {query}
order by TotalRank
LIMIT {limit} OFFSET {offset}";
        }

        #endregion 

        public void RemoveFromIndex(long urlID)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                //first delete all FTS entries for this
                SqliteCommand cmd = new SqliteCommand(@"DELETE From FTS WHERE ROWID = $urlid", connection);
                cmd.Parameters.Add(new SqliteParameter("$urlid", urlID));
                cmd.ExecuteNonQuery();

                cmd = new SqliteCommand(@"DELETE From ImageSearch WHERE ROWID = $urlid", connection); 
                cmd.Parameters.Add(new SqliteParameter("$urlid", urlID));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Ensures that virual FTS tables exist for text and image search
        /// </summary>
        private void EnsureFullTextSearch()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var cmd = new SqliteCommand("SELECT Count(*) FROM sqlite_master WHERE type='table' AND name='FTS';", connection);
                var count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count == 0)
                {
                    cmd.CommandText = "CREATE VIRTUAL TABLE FTS using fts5(Title, Body, tokenize = 'porter');";
                    cmd.ExecuteNonQuery();
                }

                cmd.CommandText = "SELECT Count(*) FROM sqlite_master WHERE type='table' AND name='ImageSearch';";
                count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count == 0)
                {
                    cmd.CommandText = "CREATE VIRTUAL TABLE ImageSearch using fts5(Terms, tokenize = 'porter');";
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}

