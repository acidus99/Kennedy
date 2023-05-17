﻿using System;
using System.Collections.Generic;

using Microsoft.Data.Sqlite;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Kennedy.SearchIndex.Search
{
	public class SearchDatabase : ISearchDatabase
	{
        string connectionString;

        public SearchDatabase(string storageDirectory)
        {
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
                    cmd.Parameters.Add(new SqliteParameter("$title", title));
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

        public List<ImageSearchResult> DoImageSearch(string query, int offset, int limit)
        {

            var ret = new List<ImageSearchResult>();
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var cmd = new SqliteCommand(
@"
Select img.UrlID, url, BodySize, Emoji, Width, Height, ImageType, IsTransparent, snippet(ImageSearch, 0, '[',']','…',20) as snip, ( rank + (rank*0.3*PopularityRank)) as tot
From ImageSearch as fts
 Inner Join Images as img
On img.UrlID = fts.ROWID
 Inner join Documents as doc
On doc.UrlID = img.UrlID
left join Favicons as f
on doc.Protocol = f.Protocol and doc.Port = f.Port and doc.Domain = f.Domain 
WHERE Terms match $query
order by tot
LIMIT $limit OFFSET $offset
", connection);

                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    cmd.Parameters.Add(new SqliteParameter("limit", limit));
                    cmd.Parameters.Add(new SqliteParameter("$offset", offset));
                    SqliteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        ret.Add(new ImageSearchResult
                        {
                            Url = new GeminiUrl(reader.GetString(reader.GetOrdinal("Url"))),
                            BodySize = reader.GetInt32(reader.GetOrdinal("BodySize")),
                            Snippet = reader.GetString(reader.GetOrdinal("snip")),
                            UrlID = reader.GetInt64(reader.GetOrdinal("UrlID")),
                            Favicon = reader["Emoji"] as string,
                            Width = reader.GetInt32(reader.GetOrdinal("Width")),
                            Height = reader.GetInt32(reader.GetOrdinal("Height")),
                            ImageType = reader.GetString(reader.GetOrdinal("ImageType")).ToUpper()
                        });
                    }
                    return ret;
                }
            }
            catch (Exception)
            {
            }
            return ret;
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
            List<FullTextSearchResult> ret = new List<FullTextSearchResult>();
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    SqliteCommand cmd = new SqliteCommand(GetAlgorithmString(), connection);

                    cmd.Parameters.Add(new SqliteParameter("$query", query));
                    cmd.Parameters.Add(new SqliteParameter("limit", limit));
                    cmd.Parameters.Add(new SqliteParameter("$offset", offset));
                    SqliteDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        ret.Add(new FullTextSearchResult
                        {
                            Url = new GeminiUrl(reader.GetString(reader.GetOrdinal("Url"))),
                            BodySize = reader.GetInt32(reader.GetOrdinal("BodySize")),
                            Title = reader["Title"] as string,
                            Snippet = reader.GetString(reader.GetOrdinal("snip")),
                            UrlID = reader.GetInt64(reader.GetOrdinal("UrlID")),
                            Language = reader["DetectedLanguage"] as string,
                            LineCount = reader.GetInt32(reader.GetOrdinal("LineCount")),
                            Favicon = reader["Emoji"] as string,
                            ExternalInboundLinks = reader.GetInt32(reader.GetOrdinal("ExternalInboundLinks")),

                            FtsRank = reader.GetDouble(reader.GetOrdinal("rank")),
                            PopRank = reader.GetDouble(reader.GetOrdinal("PopularityRank")),
                            TotalRank = reader.GetDouble(reader.GetOrdinal("tot")),
                        });
                    }
                    return ret;
                }
            }
            catch (Exception)
            {

            }
            return ret;
        }
        private string GetAlgorithmString()
        {
            return @"
Select Url, BodySize, doc.Title, UrlID, DetectedLanguage, LineCount, MimeType,  Emoji,  ExternalInboundLinks, PopularityRank, rank, ( rank + (rank*0.3*PopularityRank)) as tot, snippet(FTS, 1, '[',']','…',20) as snip
From FTS as fts
Inner Join Documents as doc
On doc.UrlID = fts.ROWID
left join Favicons as f on doc.Protocol = f.Protocol and doc.Port = f.Port and doc.Domain = f.Domain 
WHERE Body match $query
order by tot
LIMIT $limit OFFSET $offset";
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

