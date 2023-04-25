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
namespace Kennedy.AdminConsole.Importers
{
    /// <summary>
    /// Given the crawl-data stored in the modern Kennedy crawl format, import it into the archive
    /// </summary>
	public class ModernImporter
	{
		Archiver Archiver;
		string CrawlLocation;

        DocumentDbContext db;
        IDocumentStore documentStore;

        Dictionary<string, RobotsTxtFile> RobotsCache;

        public ModernImporter(Archiver archiver, string crawlLocation)
		{
			Archiver = archiver;
			CrawlLocation = crawlLocation;
            RobotsCache = new Dictionary<string, RobotsTxtFile>();
		}

		public void Import()
		{
            db = new DocumentDbContext(CrawlLocation);
            documentStore = new DocumentStore(CrawlLocation + "page-store/");

            ImportDocuments();
            //ImportDomains();
        }

        private void ImportDocuments()
        {
            int count = 0;
            try
            {
                var docs = db.Documents.Where(x => (x.ConnectStatus == ConnectStatus.Success)).ToArray();

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

                    bool isPublic = true;

                    if (doc.Status == 20 && doc.BodySaved && doc.GeminiUrl.Hostname != "kennedy.gemi.dev")
                    {
                        var data = documentStore.GetDocument(doc.UrlID);
                        if (Archiver.ArchiveResponse(doc.FirstSeen, doc.GeminiUrl, doc.Status.Value, doc.Meta, data, isPublic))
                        {
                            added++;
                        }
                    }
                    //we do want to save redirects, status prompts, and auth prompts, since those are interesting
                    else if (GeminiParser.IsInputStatus(doc.Status.Value)
                        || GeminiParser.IsRedirectStatus(doc.Status.Value)
                        || GeminiParser.IsAuthStatus(doc.Status.Value))
                    {
                        if (Archiver.ArchiveResponse(doc.FirstSeen, doc.GeminiUrl, doc.Status.Value, doc.Meta, isPublic))
                        {
                            added++;
                        }
                    }
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
    }
}

