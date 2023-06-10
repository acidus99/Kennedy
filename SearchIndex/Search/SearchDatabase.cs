using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Web;
using System.Text.RegularExpressions;

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

        private WebDatabaseContext GetContext()
            => new WebDatabaseContext(storageDirectory);

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

        private void UpdateTextIndex(long urlID, string? title, string filteredBody)
        {
            using (var db = GetContext())
            {
                //first delete all FTS entries for this
                db.Database.ExecuteSql($"DELETE From FTS as fts WHERE fts.ROWID = {urlID}");
                db.Database.ExecuteSql($"INSERT INTO FTS(ROWID, Title, Body) VALUES ({urlID}, {title}, {filteredBody})");
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

        public string? GetImageIndexText(long urlID)
        {
            using (var db = GetContext())
            {
                return db.Database.SqlQuery<string>($"Select Terms as Value From ImageSearch WHERE ROWID = {urlID}").FirstOrDefault();
            }
        }

        public int GetImageResultsCount(UserQuery query)
        {
            using (var db = GetContext())
            {
                return db.Database.SqlQuery<int>($"Select count(*) as Value From ImageSearch WHERE Terms match {query.FTSQuery}").First();
            }
        }

        private FormattableString GetImageSearchQuery(UserQuery query, int offset, int limit)
        {
            var queryParam = new SqliteParameter("query", query.FTSQuery);
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
LIMIT {limitParam} OFFSET {offsetParam}";
        }

        public List<ImageSearchResult> DoImageSearch(UserQuery query, int offset, int limit)
        {
            using (var db = GetContext())
            {
                var sql = GetImageSearchQuery(query, offset, limit);
                var results = db.ImageResults.FromSql(sql);
                return results.ToList();
            }
        }

        #endregion

        #region Text search

        public int GetTextResultsCount(UserQuery query)
        {
            using(var db = GetContext())
            {
                return db.Database.SqlQuery<int>($"Select count(*) as Value From FTS WHERE Body match {query.FTSQuery}").First();
            }
        }

        public List<FullTextSearchResult> DoTextSearch(UserQuery query, int offset, int limit)
        {
            using (var db = GetContext())
            {
                var sql = GetTextSearchQuery(query, offset, limit);
                var results = db.FtsResults.FromSql(sql);
                return results.ToList();
            }
        }

        private FormattableString GetTextSearchQuery(UserQuery query, int offset, int limit)
        {
            var queryParam = new SqliteParameter("query", query.FTSQuery);
            var offsetParam = new SqliteParameter("offset", offset);
            var limitParam = new SqliteParameter("limit", limit);

            return
$@"Select Url, BodySize, doc.Title, UrlID, DetectedLanguage, LineCount, MimeType, ( rank + (rank*0.3*PopularityRank)) as TotalRank, snippet(FTS, 1, '[',']','…',20) as Snippet
From FTS as fts
Inner Join Documents as doc
On doc.UrlID = fts.ROWID
WHERE Body MATCH {queryParam}
order by TotalRank
LIMIT {limitParam} OFFSET {offsetParam}";
        }

        #endregion 

        public void RemoveFromIndex(long urlID)
        {
            using(var db = GetContext())
            {
                db.Database.ExecuteSql($"DELETE From FTS WHERE ROWID = {urlID}");
                db.Database.ExecuteSql($"DELETE From ImageSearch WHERE ROWID = {urlID}");
            }
        }

        /// <summary>
        /// Ensures that virual FTS tables exist for text and image search
        /// </summary>
        private void EnsureFullTextSearch()
        {
            using(var db = GetContext())
            {
                var count = db.Database.SqlQuery<int>($"SELECT Count(*) as Value FROM sqlite_master WHERE type = 'table' AND name = 'FTS'").First();
                if(count == 0)
                {
                    db.Database.ExecuteSql($"CREATE VIRTUAL TABLE FTS using fts5(Title, Body, tokenize = 'porter');");
                }
                count = db.Database.SqlQuery<int>($"SELECT Count(*) as VALUE FROM sqlite_master WHERE type = 'table' AND name = 'ImageSearch'").First();
                if (count == 0)
                {
                    db.Database.ExecuteSql($"CREATE VIRTUAL TABLE ImageSearch using fts5(Terms, tokenize = 'porter');");
                }
            }
        }
    }
}

