using System.Collections.Generic;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Search
{
    internal class ImageIndexer
    {
        Dictionary<long, string> imageTextContent;
        PathTokenizer pathTokenizer;
        SqliteConnection connection;

        public ImageIndexer(string connectionString)
        {
            connection = new SqliteConnection(connectionString);
            pathTokenizer = new PathTokenizer();
            imageTextContent = new Dictionary<long, string>();
        }

        public void IndexImages()
        {
            RemoveOldIndex();
            GetContent();
            InsertText();
        }

        private void RemoveOldIndex()
        {
            connection.Open();
            //first delete all FTS entries for this
            SqliteCommand cmd = new SqliteCommand(@"DELETE From ImageSearch", connection);
            cmd.ExecuteNonQuery();
            connection.Close();
        }

        private void GetContent()
        {
            connection.Open();
            using (var cmd = new SqliteCommand(@"select Images.UrlID, LinkText, url from Images
Inner Join Documents
on Documents.UrlID = Images.UrlID
INNER Join Links
on Images.UrlID = Links.TargetUrlID
where length(LinkText) >= 0", connection))
            {
                var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    long urlID = r.GetInt64(r.GetOrdinal("UrlID"));
                    string url = r.GetString(r.GetOrdinal("Url"));
                    string linkText = r.GetString(r.GetOrdinal("LinkText"));
                    if (!imageTextContent.ContainsKey(urlID))
                    {
                        imageTextContent[urlID] = GetPathIndexText(url);
                    }
                    if (linkText.Length > 0)
                    {
                        imageTextContent[urlID] += CleanLinkText(linkText) + " ";
                    }
                }
            }
            connection.Close();
        }

        private void InsertText()
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                var command = connection.CreateCommand();
                //Insert into DocsFTS(ROWID, Title, Body) Values (1, 'BBC News', 'News of the world! People are happy and are living longer. Cat ownership is up!');
                command.CommandText = @"INSERT INTO ImageSearch(ROWID, Terms) VALUES ($docid, $terms)";

                SqliteParameter parameterDbDocID = command.CreateParameter();
                parameterDbDocID.ParameterName = "$docid";
                command.Parameters.Add(parameterDbDocID);

                SqliteParameter parameterTerms = command.CreateParameter();
                parameterTerms.ParameterName = "$terms";
                command.Parameters.Add(parameterTerms);

                foreach (var dbDocID in imageTextContent.Keys)
                {
                    parameterDbDocID!.Value = dbDocID;
                    parameterTerms!.Value = imageTextContent[dbDocID];
                    command!.ExecuteNonQuery();
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
    }
}