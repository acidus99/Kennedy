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
            if(parsedResponse.IsIndexable)
            {

                switch(parsedResponse.ContentType)
                {
                    case ContentType.Gemtext:
                        {
                            var doc = (GemTextResponse) parsedResponse;
                            UpdateTextIndex(parsedResponse.RequestUrl.ID, doc.Title, doc.FilteredBody);
                            break;
                        }

                    case ContentType.PlainText:
                        {
                            var doc = (PlainTextResponse)parsedResponse;
                            UpdateTextIndex(parsedResponse.RequestUrl.ID, null, doc.BodyText);
                            break;
                        }
                }
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

        public int GetImageResultsCount(UserQuery userQuery)
        {
            using (var db = GetContext())
            {
                var sqlQuery = new DynamicQuery<SqliteParameter>();
                sqlQuery.Append("Select count(*) as Value From ImageSearch ");

                //if we have other things besides term queries, we need to do a join

                if (userQuery.HasSiteScope)
                {
                    sqlQuery.Append("Inner Join Documents On Documents.UrlID = ImageSearch.ROWID ");
                }
                sqlQuery.Append("WHERE Terms MATCH {} ");
                sqlQuery.AddParameter("query", userQuery.FTSQuery);

                if (userQuery.HasSiteScope)
                {
                    sqlQuery.Append("AND domain = {} ");
                    sqlQuery.AddParameter("domain", userQuery.SiteScope);
                }

                return db.Database.SqlQuery<int>(sqlQuery.GetFormattableString()).First();
            }
        }

        private FormattableString GetImageSearchQuery(UserQuery query, int offset, int limit)
        {
            var sqlQuery = new DynamicQuery<SqliteParameter>();

            sqlQuery.Append(@"
Select img.UrlID, url, BodySize, Width, Height, ImageType, snippet(ImageSearch, 0, '[',']','…',20) as Snippet, ( rank + (rank*0.3*PopularityRank)) as tot
From ImageSearch as fts
 Inner Join Images as img
On img.UrlID = fts.ROWID
 Inner join Documents as doc
On doc.UrlID = img.UrlID
WHERE Terms match {} ");

            sqlQuery.AddParameter("query", query.FTSQuery);
            if(query.HasSiteScope)
            {
                sqlQuery.Append("AND domain = {} ");
                sqlQuery.AddParameter("domain", query.SiteScope);
            }

            sqlQuery.Append("ORDER BY tot LIMIT {} OFFSET {}");
            sqlQuery.AddParameter("limit", limit);
            sqlQuery.AddParameter("offset", offset);

            return sqlQuery.GetFormattableString();
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

        public int GetTextResultsCount(UserQuery userQuery)
        {
            using(var db = GetContext())
            {
                var sqlQuery = new DynamicQuery<SqliteParameter>();
                sqlQuery.Append("Select count(*) as Value From FTS ");

                //if we have other things besides term queries, we need to do a join

                if(userQuery.HasSiteScope)
                {
                    sqlQuery.Append("Inner Join Documents On Documents.UrlID = fts.ROWID ");
                }
                sqlQuery.Append("WHERE Body MATCH {} ");
                sqlQuery.AddParameter("query", userQuery.FTSQuery);

                if(userQuery.HasSiteScope)
                {
                    sqlQuery.Append("AND domain = {} ");
                    sqlQuery.AddParameter("domain", userQuery.SiteScope);
                }

                return db.Database.SqlQuery<int>(sqlQuery.GetFormattableString()).First();
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

        private FormattableString GetTextSearchQuery(UserQuery userQuery, int offset, int limit)
        {
            var sqlQuery = new DynamicQuery<SqliteParameter>();

            sqlQuery.Append(@"
Select Url, BodySize, doc.Title, UrlID, DetectedLanguage, LineCount, MimeType, ( rank + (rank*0.3*PopularityRank)) as TotalRank, snippet(FTS, 1, '[',']','…',20) as Snippet
From FTS as fts
Inner Join Documents as doc
On doc.UrlID = fts.ROWID
Where Body MATCH {} ");

            sqlQuery.AddParameter("query", userQuery.FTSQuery);
            
            if (userQuery.HasSiteScope)
            {
                sqlQuery.Append("AND domain = {} ");
                sqlQuery.AddParameter("domain", userQuery.SiteScope);
            }

            sqlQuery.Append("order by TotalRank LIMIT {} OFFSET {} ");

            sqlQuery.AddParameter("limit", limit);
            sqlQuery.AddParameter("offset", offset);

            return sqlQuery.GetFormattableString();
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

