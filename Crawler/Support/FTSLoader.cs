using System;
using System.Linq;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Gemini.Net;
using Kennedy.Crawler.GemText;
using System.Text;
using Microsoft.Data.Sqlite;


namespace Kennedy.Crawler.Support
{
    public class FTSLoader
    {
        SqliteCommand command;
        SqliteConnection connection;
        SqliteParameter parameterDbDocID;
        SqliteParameter parameterBody;
        SqliteParameter parameterTitle;

        DocIndexDbContext db;
        DocumentStore docStore;


        public FTSLoader(string dataDir)
        {
            db = new DocIndexDbContext(dataDir);
            docStore = new DocumentStore(dataDir + "page-store/");
            connection = new SqliteConnection($"Data Source='{dataDir}doc-index.db'");
        }

        public void LoadDocuments()
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {

                command = connection.CreateCommand();
                //Insert into DocsFTS(ROWID, Title, Body) Values (1, 'BBC News', 'News of the world! People are happy and are living longer. Cat ownership is up!');
                command.CommandText = @"INSERT INTO DocumentFTS(ROWID, Title, Body) VALUES ($docid, $title, $body)";

                parameterDbDocID = command.CreateParameter();
                parameterDbDocID.ParameterName = "$docid";
                command.Parameters.Add(parameterDbDocID);

                parameterTitle = command.CreateParameter();
                parameterTitle.ParameterName = "$title";
                command.Parameters.Add(parameterTitle);

                parameterBody = command.CreateParameter();
                parameterBody.ParameterName = "$body";
                command.Parameters.Add(parameterBody);

                var entries = db.DocEntries
                        .Where(x => x.BodySaved && x.MimeType.StartsWith("text/gemini"))
                        .Select(x => new
                        {
                            DbDocID = x.DBDocID,
                            Title = x.Title
                        }).ToList();

                int count = entries.Count;
                int curr = 0;

                foreach (var entry in entries)
                {
                    curr++;
                    if(curr % 100 == 0)
                    {
                        Console.WriteLine($"{curr}\t{count}");
                    }
                    LoadDocumentIntoFTS(entry.DbDocID, entry.Title);
                }
                transaction.Commit();
            }



        }

        private void LoadDocumentIntoFTS(long dbDocID, string title)
        {
            string text = GetDocumentText(dbDocID);
            text = FilterBody(text);
            StoreInDB(dbDocID, title, text);
        }

        private string GetDocumentText(long dbDocID)
        {
            ulong docID = DocumentIndex.toULong(dbDocID);

            byte [] bytes = docStore.GetDocument(docID);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private void StoreInDB(long dbDocID, string title, string text)
        {
            parameterDbDocID.Value = dbDocID;
            parameterTitle.Value = title;
            parameterBody.Value = text;
            command.ExecuteNonQuery();
        }


        /// <summary>
        /// gets rid of preformatted text, and the hyperlink part of any link lines
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private string FilterBody(string text)
        {
            var sb = new StringBuilder();
            foreach(string line in LineParser.RemovePreformatted(text))
            {
                if(line.StartsWith("=>"))
                {
                    sb.AppendLine(LinkFinder.GetLinkText(line));
                } else
                {
                    sb.AppendLine(line);
                }
            }
            return sb.ToString();
        }

        
    }
}
