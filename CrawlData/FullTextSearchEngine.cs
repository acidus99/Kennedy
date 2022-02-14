using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

using Gemini.Net;

namespace Kennedy.CrawlData
{
    public class FullTextSearchEngine
    {

        string connectString;

        public FullTextSearchEngine(string storageDirectory)
        {
            connectString = $"Data Source='{storageDirectory}doc-index.db'";
        }

        public int GetResultsCount(string query)
        {
            try
            {
                using (var connection = new SqliteConnection(connectString))
                {
                    connection.Open();
                    SqliteCommand cmd = new SqliteCommand(@"Select count(*) From FTS WHERE Body match $query", connection);
                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            } catch(Exception)
            {

            }
            return 0;
        }

        public List<FullTextSearchResult> DoSearch(string query, int offset, int limit)
        {
            List<FullTextSearchResult> ret = new List<FullTextSearchResult>();
            try
            {
                using (var connection = new SqliteConnection(connectString))
                {
                    connection.Open();
                    SqliteCommand cmd = new SqliteCommand(@"
Select Url, BodySize, doc.Title, DBDocID, Language, LineCount, HasFaviconTxt, FaviconTxt, snippet(FTS, 1, '[',']','…',20) as snip
From FTS as fts
Inner Join Documents as doc
On doc.DBDocID = fts.ROWID
Inner Join Domains as dom
On doc.Domain = dom.Domain
WHERE Body match $query
order by rank
LIMIT $limit OFFSET $offset
", connection);

                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    cmd.Parameters.Add(new SqliteParameter("limit", limit));
                    cmd.Parameters.Add(new SqliteParameter("$offset", offset));
                    SqliteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {

                        var favicon = reader.GetBoolean(reader.GetOrdinal("HasFaviconTxt"))
                            ? reader["FaviconTxt"].ToString() :
                            "";

                        ret.Add(new FullTextSearchResult
                        {
                            Url = new GeminiUrl(reader["Url"].ToString()),
                            BodySize = reader.GetInt32(reader.GetOrdinal("BodySize")),
                            Title = reader["Title"].ToString(),
                            Snippet = reader["snip"].ToString(),
                            DBDocID = reader.GetInt64(reader.GetOrdinal("DBDocID")),
                            Language = reader["Language"].ToString(),
                            LineCount = reader.GetInt32(reader.GetOrdinal("LineCount")),
                            Favicon = favicon
                        }); ;
                    }
                    return ret;
                }
            } catch(Exception)
            {

            }
            return ret;
        }

        public void AddResponseToIndex(long dbDocID, string title,string filteredBody)
        {
            using (var connection = new SqliteConnection(connectString))
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
                    cmd.Parameters.Add(new SqliteParameter("$title", title));
                    cmd.Parameters.Add(new SqliteParameter("$body", filteredBody));
                    cmd.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }
    }
}
