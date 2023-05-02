using System;
using System.Net.NetworkInformation;
using System.Text.Encodings;

using Gemini.Net;

using Kennedy.Archive;
using Kennedy.Data.RobotsTxt;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Storage;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;

using Kennedy.AdminConsole.Db;
using Kennedy.Warc;

using Kennedy.AdminConsole.Importers;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.Data.Sqlite;

namespace Kennedy.AdminConsole.Converters
{
    /// <summary>
    /// Converts the "Documents" table from Kennedy Crawls into WARC files
    /// </summary>
	public class BareConverter
	{
		string CrawlLocation;

        DocumentDbContext db;
        IDocumentStore documentStore;
        GeminiWarcCreator WarcCreator;

        string ftsTable;

        public BareConverter(GeminiWarcCreator warcCreator, string crawlLocation)
		{
			CrawlLocation = crawlLocation;
            db = new DocumentDbContext(CrawlLocation);
            WarcCreator = warcCreator;
            ftsTable = GetFTSTableName();
		}

        private string GetFTSTableName()
        {

            if (TableExists("DocumentFTS"))
            {
                return "DocumentFTS";
            }
            else
            {
                return "FTS";
            }
        }

        private bool TableExists(string name)
        {
            using (var connection = new SqliteConnection(db.Database.GetConnectionString()))
            {
                connection.Open();
                var cmd = new SqliteCommand($"SELECT Count(*) FROM sqlite_schema WHERE type ='table' and name = '{name}';", connection);
                var count = Convert.ToInt32(cmd.ExecuteScalar());

                return count == 1;
            }
        }

        private string GetBodyText(long dbDocID)
        {
            using (var connection = new SqliteConnection(db.Database.GetConnectionString()))
            {
                connection.Open();
                var cmd = new SqliteCommand($"SELECT Body FROM {ftsTable} where ROWID = {dbDocID}", connection);
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return reader[0].ToString();
                }
                else
                {
                    return null;
                }
            }
        }


        public void ToWarc()
		{
            
            documentStore = new DocumentStore(CrawlLocation + "page-store/");

            int DocumentEntrys = 0;
            int warcResponses = 0;
            int warcResponsesTruncated = 0;

            int count = 0;
            try
            {
                var docs = db.Documents.ToArray();
                DocumentEntrys = docs.Length;
                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                int added = 0;
                foreach (var doc in docs)
                {
                    try
                    {
                        //some older crawls had bad URLs and ports in them, so skip those
                        //e.g.: gemini://300:de6b:9ffb:a959::d:1965/
                        var dummy = doc.GeminiUrl;

                     } catch(Exception ex)
                    {
                        continue;
                    }

                    byte[] data = null;
                    string bodyText = GetBodyText(doc.UrlID);
                    if(bodyText?.Length > 0)
                    {
                        int x = 4;
                        data = System.Text.Encoding.UTF8.GetBytes(bodyText);
                    }

                    count++;
                    if (count % 100 == 0)
                    {
                        Console.WriteLine($"Crawl: {CrawlLocation}: Processed {count} of {docs.Length}. Added to archive: {added}");
                    }

                    bool isTruncated = false;

                    //older crawls didn't explicitly set a status code for connection errors, so do that now
                    if(doc.ConnectStatus == ConnectStatus.Error && doc.Status != 20) 
                    {
                        doc.Status = 49;
                    }

                    //we don't actually have responseReceived times, so approximate them

                    if (DownloadWasSkipped(doc, data))
                    {
                        warcResponsesTruncated++;
                        WarcCreator.RecordTruncatedSession(doc.FirstSeen, doc.GeminiUrl, doc.FirstSeen.AddSeconds(2), doc.Status.Value, doc.MimeType, data);
                    }
                    else
                    {
                        warcResponses++;
                        WarcCreator.RecordSession(doc.FirstSeen, doc.GeminiUrl, doc.FirstSeen.AddSeconds(2), doc.Status.Value, doc.Meta, data);
                    }
                    added++;
                }
                watch.Stop();
                Console.WriteLine($"Completed processing {CrawlLocation}");
                Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
                Console.WriteLine($"--");
                Console.WriteLine($"Docs:\t{DocumentEntrys}");
                Console.WriteLine($"resps:\t{warcResponses}");
                Console.WriteLine($"Tresps:\t{warcResponsesTruncated}");
                Console.WriteLine($"Added:\t{added}");
            }
            catch (Exception ex)
            {
                int x = 4;
            }
        }

        private bool DownloadWasSkipped(SimpleDocument document, byte [] data)
        {
            if(document?.Status == 20 && data == null)
            {
                return true;
            }

            return false;
        }

    }
}

