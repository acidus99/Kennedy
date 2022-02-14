using System;
using System.Linq;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Gemini.Net;
using Kennedy.Crawler.GemText;
using System.Text;
using Microsoft.Data.Sqlite;
using NTextCat;


namespace Kennedy.Crawler.Support
{
    public class LineLoader
    {
        SqliteCommand command;
        SqliteConnection connection;

        DocIndexDbContext db;
        DocumentStore docStore;


        public LineLoader(string dataDir)
        {
            db = new DocIndexDbContext(dataDir);
            docStore = new DocumentStore(dataDir + "page-store/");
            connection = new SqliteConnection($"Data Source='{dataDir}doc-index.db'");
        }

        public void LoadDocuments()
        {
            // Don't forget to deploy a language profile (e.g. Core14.profile.xml) with your application.
            // (take a look at "content" folder inside of NTextCat nupkg and here: https://github.com/ivanakcheurov/ntextcat/tree/master/src/LanguageModels).
            var factory = new RankedLanguageIdentifierFactory();
            var identifier = factory.Load("Core14.profile.xml"); // can be an absolute or relative path. Beware of 260 chars limitation of the path length in Windows. Linux allows 4096 chars.

            int counter = 0;

            var entries = db.DocEntries
                    .Where(x => x.BodySaved && x.MimeType.StartsWith("text/gemini")).ToList();

            foreach (var entry in entries)
            {
                counter++;

                if(counter % 100 == 0)
                {
                    Console.WriteLine($"{counter}\t{entries.Count}");
                }

                var docText = GetDocumentText(entry.DBDocID);
                var lines = docText.Split("\n").Count();
                
                var lang = "";

                if (entry.BodySize > 150)
                {
                    var filtered = FilterBody(docText);

                    if (filtered.Length > 150)
                    {
                        var mostCertainLanguage = identifier.Identify(filtered).FirstOrDefault();
                        lang = (mostCertainLanguage != null) ? mostCertainLanguage.Item1.Iso639_3 : "";
                    }
                }

                entry.LineCount = lines;
                entry.Language = lang;
            }
            db.SaveChanges();


        }

        private string GetDocumentText(long dbDocID)
        {
            ulong docID = DocumentIndex.toULong(dbDocID);

            byte [] bytes = docStore.GetDocument(docID);
            return System.Text.Encoding.UTF8.GetString(bytes);
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
