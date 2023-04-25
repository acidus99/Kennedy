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

namespace Kennedy.AdminConsole.Converters
{
    /// <summary>
    /// Converts the "Documents" table from Kennedy Crawls into WARC files
    /// </summary>
	public class DocumentConverter
	{
		string CrawlLocation;

        DocumentDbContext db;
        IDocumentStore documentStore;
        GeminiWarcCreator WarcCreator;

        public DocumentConverter(GeminiWarcCreator warcCreator, string crawlLocation)
		{
			CrawlLocation = crawlLocation;
            WarcCreator = warcCreator;
		}

		public void ToWarc()
		{
            db = new DocumentDbContext(CrawlLocation);
            documentStore = new DocumentStore(CrawlLocation + "page-store/");

            int count = 0;
            try
            {
                var docs = db.Documents.ToArray();

                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                int added = 0;
                foreach (var doc in docs)
                {
                    count++;
                    if (count % 100 == 0)
                    {
                        Console.WriteLine($"Crawl: {CrawlLocation}: Processed {count} of {docs.Length}. Added to archive: {added}");
                    }

                    bool isTruncated = false;

                    byte[] data = null;
                    if(doc.ConnectStatus == ConnectStatus.Error)
                    {
                        doc.Status = 49;
                    }

                    if (doc.BodySaved)
                    {
                        data = documentStore.GetDocument(doc.UrlID);
                    }

                    if (DownloadWasSkipped(doc))
                    {
                        WarcCreator.RecordTruncatedSession(doc.FirstSeen, doc.GeminiUrl, doc.Status.Value, doc.MimeType);
                    }
                    else
                    {
                        WarcCreator.RecordSession(doc.FirstSeen, doc.GeminiUrl, doc.Status.Value, doc.Meta, data);
                    }
                    added++;
                }
                watch.Stop();
                Console.WriteLine($"Completed processing {CrawlLocation}");
                Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
                Console.WriteLine($"Snapshots Added:\t{added}");
            }
            catch (Exception ex)
            {
                int x = 4;
            }
        }

        private bool DownloadWasSkipped(SimpleDocument document)
        {
            if(document?.Status == 20 && document.ConnectStatus == ConnectStatus.Error)
            {
                return true;
            }
            return false;
        }

    }
}

