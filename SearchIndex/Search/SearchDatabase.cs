using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Kennedy.Data;
using Kennedy.Data.Utils;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Web;

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

        private bool ShouldIndexText(ParsedResponse parsedResponse)
        {
            if (parsedResponse is ITextResponse textDoc)
            {
                //only index text responses, with indexable text, that are also not proactive requests
                if (!UrlUtility.IsProactiveUrl(parsedResponse.RequestUrl.Url))
                {
                    return textDoc.HasIndexableText;
                }
            }
            return false;
        }

        public void UpdateIndex(ParsedResponse parsedResponse)
        {
            if(ShouldIndexText(parsedResponse) && parsedResponse is ITextResponse)
            {
                var textDoc = (ITextResponse)parsedResponse;

                if (textDoc.HasIndexableText)
                {
                    UpdateIndexForUrl(parsedResponse.RequestUrl.ID, textDoc.IndexableText!, textDoc.Title);
                }
            }
        }

        public void UpdateIndexForUrl(long urlID, string filteredBody, string? title = null)
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

        public void IndexFiles()
        {
            FileIndexer fileIndexer = new FileIndexer(storageDirectory, this);
            fileIndexer.IndexFiles();

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
            if(!userQuery.IsValidImageQuery)
            {
                throw new ArgumentException("Not a valid query for image search");
            }

            using (var db = GetContext())
            {
                var sqlQuery = new DynamicQuery<SqliteParameter>();
                sqlQuery.Append("Select count(*) as Value From ImageSearch ");

                //if we have other things besides term queries, we need to do a join

                if (userQuery.HasSiteScope || userQuery.HasFileTypeScope || userQuery.HasUrlScope)
                {
                    sqlQuery.Append("Inner Join Documents On Documents.UrlID = ImageSearch.ROWID ");
                }
                sqlQuery.Append("WHERE ");

                if (userQuery.HasFtsQuery)
                {
                    sqlQuery.AppendWhereCondition("Terms MATCH {} ");
                    sqlQuery.AddParameter("query", userQuery.FTSQuery);
                }

                if (userQuery.HasSiteScope)
                {
                    sqlQuery.AppendWhereCondition("domain = {} ");
                    sqlQuery.AddParameter("domain", userQuery.SiteScope);
                }

                if (userQuery.HasFileTypeScope)
                {
                    sqlQuery.AppendWhereCondition("FileExtension = {} ");
                    sqlQuery.AddParameter("filetype", userQuery.FileTypeScope);
                }

                if(userQuery.HasUrlScope)
                {
                    sqlQuery.AppendWhereCondition($"Path LIKE '%{userQuery.UrlScope}%' ");
                }

                var sql = sqlQuery.GetFormattableString();

                return db.Database.SqlQuery<int>(sql).First();
            }
        }

        private FormattableString GetImageSearchQuery(UserQuery userQuery, int offset, int limit)
        {
            if (!userQuery.IsValidImageQuery)
            {
                throw new ArgumentException("Not a valid query for image search");
            }

            var sqlQuery = new DynamicQuery<SqliteParameter>();

            sqlQuery.Append(@"
Select img.UrlID, url, BodySize, IsBodyTruncated, Width, Height, ImageType, snippet(ImageSearch, 0, '[',']','…',20) as Snippet, ( rank + (rank*0.3*PopularityRank)) as tot
From ImageSearch as fts
 Inner Join Images as img
On img.UrlID = fts.ROWID
 Inner join Documents as doc
On doc.UrlID = img.UrlID
WHERE ");

            if (userQuery.HasFtsQuery)
            {
                sqlQuery.AppendWhereCondition("Terms MATCH {} ");
                sqlQuery.AddParameter("query", userQuery.FTSQuery);
            }

            if (userQuery.HasSiteScope)
            {
                sqlQuery.AppendWhereCondition("domain = {} ");
                sqlQuery.AddParameter("domain", userQuery.SiteScope);
            }

            if (userQuery.HasFileTypeScope)
            {
                sqlQuery.AppendWhereCondition("FileExtension = {} ");
                sqlQuery.AddParameter("filetype", userQuery.FileTypeScope);
            }

            if (userQuery.HasUrlScope)
            {
                sqlQuery.AppendWhereCondition($"Path LIKE '%{userQuery.UrlScope}%' ");
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
            if (!userQuery.IsValidTextQuery)
            {
                throw new ArgumentException("Not a valid query for textsearch");
            }

            using (var db = GetContext())
            {
                var sqlQuery = new DynamicQuery<SqliteParameter>();
                sqlQuery.Append("Select count(*) as Value From FTS ");

                //if we have other things besides term queries, we need to do a join

                if(userQuery.HasSiteScope || userQuery.HasFileTypeScope || userQuery.HasUrlScope)
                {
                    sqlQuery.Append("Inner Join Documents On Documents.UrlID = fts.ROWID ");
                }

                sqlQuery.Append("WHERE ");

                if(userQuery.HasFtsQuery)
                {
                    sqlQuery.AppendWhereCondition("FTS.Body MATCH {} ");
                    sqlQuery.AddParameter("query", userQuery.FTSQuery);
                }

                if(userQuery.HasTitleScope)
                {
                    sqlQuery.AppendWhereCondition("FTS.Title MATCH {} ");
                    sqlQuery.AddParameter("title", userQuery.TitleScope);
                }

                if (userQuery.HasSiteScope)
                {
                    sqlQuery.AppendWhereCondition("domain = {} ");
                    sqlQuery.AddParameter("domain", userQuery.SiteScope);
                }

                if(userQuery.HasFileTypeScope)
                {
                    sqlQuery.AppendWhereCondition("FileExtension = {} ");
                    sqlQuery.AddParameter("filetype", userQuery.FileTypeScope);
                }

                if (userQuery.HasUrlScope)
                {
                    sqlQuery.AppendWhereCondition($"Path LIKE '%{userQuery.UrlScope}%' ");
                }

                var sql = sqlQuery.GetFormattableString();

                return db.Database.SqlQuery<int>(sql).First();
            }
        }

        public List<FullTextSearchResult> DoTextSearch(UserQuery query, int offset, int limit)
        {
            if (!query.IsValidTextQuery)
            {
                throw new ArgumentException("Not a valid query for textsearch");
            }

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
Select Url, BodySize, IsBodyTruncated, doc.Title, UrlID, DetectedLanguage, LineCount, MimeType, (rank + (rank*0.3*PopularityRank)) * IIF(ContentType = 1, 1.03, 1) as TotalRank, ");

            if (userQuery.HasFtsQuery)
            {
                sqlQuery.Append("snippet(FTS, 1, '[',']','…',20) as Snippet ");
            }
            else
            {
                sqlQuery.Append("substr(Body, 0, IIF(LENGTH(Body) > 100, 100, LENGTH(BODY))) as Snippet ");
            }
            sqlQuery.Append("From FTS as fts Inner Join Documents as doc On doc.UrlID = fts.ROWID WHERE ");

            if (userQuery.HasFtsQuery)
            {
                sqlQuery.AppendWhereCondition("FTS.Body MATCH {} ");
                sqlQuery.AddParameter("query", userQuery.FTSQuery);
            }

            if (userQuery.HasTitleScope)
            {
                sqlQuery.AppendWhereCondition("FTS.Title MATCH {} ");
                sqlQuery.AddParameter("title", userQuery.TitleScope);
            }

            if (userQuery.HasSiteScope)
            {
                sqlQuery.AppendWhereCondition("domain = {} ");
                sqlQuery.AddParameter("domain", userQuery.SiteScope);
            }

            if (userQuery.HasFileTypeScope)
            {
                sqlQuery.AppendWhereCondition("FileExtension = {} ");
                sqlQuery.AddParameter("filetype", userQuery.FileTypeScope);
            }

            if (userQuery.HasUrlScope)
            {
                sqlQuery.AppendWhereCondition($"Path LIKE '%{userQuery.UrlScope}%' ");
            }

            if (userQuery.HasFtsQuery)
            {
                sqlQuery.Append("order by TotalRank ");
            }
            else
            {
                sqlQuery.Append("order by ExternalInboundLinks desc, Url ");
            }
            sqlQuery.Append("LIMIT {} OFFSET {} ");

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

