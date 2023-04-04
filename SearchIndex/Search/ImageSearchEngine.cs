using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

using Gemini.Net;

namespace Kennedy.CrawlData.Search
{
    public class ImageSearchEngine
    {
        string connectString;

        public ImageSearchEngine(string storageDirectory)
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
                    SqliteCommand cmd = new SqliteCommand(@"Select count(*) From ImageSearch WHERE Terms match $query", connection);
                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            } catch(Exception)
            {

            }
            return 0;
        }

        private string GetSearchQuery()
        => @"Select img.DBDocID, url, BodySize, BodySaved, Width, Height, ImageType, IsTransparent, HasFaviconTxt, FaviconTxt,  snippet(ImageSearch, 0, '[',']','…',20) as snip
From ImageSearch as fts
 Inner Join Images as img
On img.DBDocID = fts.ROWID
 Inner join Documents as doc
On doc.DBDocID = img.DBDocID
Inner Join Domains as dom
On doc.Domain = dom.Domain and doc.Port = dom.Port
WHERE Terms match $query
LIMIT $limit OFFSET $offset";

        public List<ImageSearchResult> DoSearch(string query, int offset, int limit)
        {
            var ret = new List<ImageSearchResult>();
            try
            {
                using (var connection = new SqliteConnection(connectString))
                {
                    connection.Open();
                    var cmd = new SqliteCommand(GetSearchQuery(), connection);

                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    cmd.Parameters.Add(new SqliteParameter("limit", limit));
                    cmd.Parameters.Add(new SqliteParameter("$offset", offset));
                    SqliteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {

                        var favicon = reader.GetBoolean(reader.GetOrdinal("HasFaviconTxt"))
                            ? reader["FaviconTxt"].ToString() :
                            "";

                        ret.Add(new ImageSearchResult
                        {
                            Url = new GeminiUrl(reader["Url"].ToString()),
                            BodySize = reader.GetInt32(reader.GetOrdinal("BodySize")),
                            Snippet = reader["snip"].ToString(),
                            DBDocID = reader.GetInt64(reader.GetOrdinal("DBDocID")),
                            BodySaved = reader.GetBoolean(reader.GetOrdinal("BodySaved")),
                            Favicon = favicon,
                            Width = reader.GetInt32(reader.GetOrdinal("Width")),
                            Height = reader.GetInt32(reader.GetOrdinal("Height")),
                            ImageType = reader["ImageType"].ToString().ToUpper()
                        });
                    }
                    return ret;
                }
            } catch(Exception)
            {

            }
            return ret;
        }
    }
}
