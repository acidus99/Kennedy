using System;
using System.Collections.Generic;
using System.Linq;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Gemini.Net;
using Kennedy.Crawler.GemText;
using System.Text;
using Microsoft.Data.Sqlite;
using System.Linq;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Crawler.Support
{
    public class ImageIndexer
    {
        Dictionary<long, string> imageTextContent;
        PathTokenizer pathTokenizer;

        SqliteCommand command;
        SqliteConnection connection;
        SqliteParameter parameterDbDocID;
        SqliteParameter parameterTerms;

        DocumentStore docStore = new DocumentStore(CrawlerOptions.DataDirectory + "page-store/");

        public ImageIndexer()
        {
            connection = new SqliteConnection($"Data Source='{CrawlerOptions.DataDirectory}doc-index.db'");
            pathTokenizer = new PathTokenizer();
            imageTextContent = new Dictionary<long, string>();
        }

        public void IndexImages()
        {
            GetContent();
            InsertText();
        }

        private void GetContent()
        {
            connection.Open();
            using (var cmd = new SqliteCommand(@"select Images.DBDocID, LinkText, url from Images
Inner Join Documents
on Documents.DBDocID = Images.DBDocID
INNER Join Links
on Images.DBDocID = Links.DBTargetDocID
where length(LinkText) > 0", connection))
            {
                var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    long dbDocID = r.GetInt64(r.GetOrdinal("DBDocID"));
                    string url = r.GetString(r.GetOrdinal("Url"));
                    string linkText = r.GetString(r.GetOrdinal("LinkText"));
                    if (!imageTextContent.ContainsKey(dbDocID))
                    {
                        imageTextContent[dbDocID] = GetPathIndexText(url);
                    }
                    imageTextContent[dbDocID] += CleanLinkText(linkText) + " ";
                }
            }
            connection.Close();
        }

        private void InsertText()
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                command = connection.CreateCommand();
                //Insert into DocsFTS(ROWID, Title, Body) Values (1, 'BBC News', 'News of the world! People are happy and are living longer. Cat ownership is up!');
                command.CommandText = @"INSERT INTO ImageSearch(ROWID, Terms) VALUES ($docid, $terms)";

                parameterDbDocID = command.CreateParameter();
                parameterDbDocID.ParameterName = "$docid";
                command.Parameters.Add(parameterDbDocID);

                parameterTerms = command.CreateParameter();
                parameterTerms.ParameterName = "$terms";
                command.Parameters.Add(parameterTerms);

                foreach (var dbDocID in imageTextContent.Keys)
                {
                    StoreInDB(dbDocID, imageTextContent[dbDocID]);
                }
                transaction.Commit();
            }
            connection.Close();
        }

        private string CleanLinkText(string s)
            => s.Trim();

        private string GetPathIndexText(string url)
        {
            string[] tokens = pathTokenizer.GetTokens(url);
            return (tokens != null) ? string.Join(' ', tokens) + " " : "";
        }

        private void StoreInDB(long dbDocID, string terms)
        {
            parameterDbDocID.Value = dbDocID;
            parameterTerms.Value = terms;
            command.ExecuteNonQuery();
        }

    }

        
}

