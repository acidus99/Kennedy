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
using Microsoft.Data.Sqlite;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Reflection.Metadata;

namespace Kennedy.AdminConsole.Converters
{
    /// <summary>
    /// Converts the "Documents" table from a bare scan database, but using the text in the FTS table
    /// </summary> 
	public class BareConverter :AbstractConverter
	{
		string CrawlLocation;

        DocumentDbContext db;
        IDocumentStore documentStore;

        string ftsTable;

        public BareConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            : base(warcCreator)
		{
			CrawlLocation = crawlLocation;
            db = new DocumentDbContext(CrawlLocation);
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


        public override void ToWarc()
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

                    count++;
                    if (count % 100 == 0)
                    {
                        Console.WriteLine($"Crawl: {CrawlLocation}: Processed {count} of {docs.Length}. Added to archive: {added}");
                    }

                    ///====== Normalize the data
                    //older crawls had a status of 0 if there was a connection error, so normalize that to our code 49
                    if (doc.Status == 0)
                    {
                        doc.Status = GeminiParser.ConnectionErrorStatusCode;
                    }

                    //fix up meta for content that was too large
                    if (doc.Status == 20 && doc.Meta.StartsWith("Requestor aborting due to reaching max download"))
                    {
                        doc.Meta = doc.MimeType;
                    }


                    //clean up the Mime to just be content-type
                    if (doc.Status == 20)
                    {
                        doc.MimeType = GetJustMimetype(doc.MimeType);
                    }

                    bool isTruncated = IsTruncated(doc);
                    byte[]? bodyBytes = null;
                    string bodyText = GetBodyText(doc.UrlID);
                    if(bodyText?.Length > 0)
                    {
                        int x = 4;
                        bodyBytes = System.Text.Encoding.UTF8.GetBytes(bodyText);
                    }
                    if (doc.Status == 20 && bodyBytes == null)
                    {
                        isTruncated = true;
                    }

                    WarcCreator.WriteLegacySession(doc.GeminiUrl, doc.FirstSeen, doc.Status, doc.Meta, doc.MimeType, bodyBytes, isTruncated);

                    if (isTruncated)
                    {
                        warcResponsesTruncated++;
                    }
                    else
                    {
                        warcResponses++;
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

    }
}

