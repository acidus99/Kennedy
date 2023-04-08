using System.Collections.Generic;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Search
{
    internal class ImageIndexer
    {
        Dictionary<long, string> imageTextContent;
        PathTokenizer pathTokenizer;

        SqliteCommand command;
        SqliteConnection connection;
        SqliteParameter parameterDbDocID;
        SqliteParameter parameterTerms;

        public ImageIndexer(string connectionString)
        {
            connection = new SqliteConnection(connectionString);
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