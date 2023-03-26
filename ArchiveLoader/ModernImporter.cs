using System;

using Kennedy.Archive;
using Kennedy.CrawlData.Db;
using Kennedy.CrawlData;

namespace ArchiveLoader
{
    /// <summary>
    /// Given the crawl-data stored in the modern Kennedy crawl format, import it into the archive
    /// </summary>
	public class ModernImporter
	{
		Archiver Archiver;
		string CrawlLocation;

		public ModernImporter(Archiver archiver, string crawlLocation)
		{
			Archiver = archiver;
			CrawlLocation = crawlLocation;
		}

		public void Import()
		{
            DocIndexDbContext db = new DocIndexDbContext(CrawlLocation);
            DocumentStore documentStore = new DocumentStore(CrawlLocation + "page-store/");

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            int count = 0;
            var docs = db.DocEntries.Where(x => (x.Status == 20 && x.BodySaved)).ToArray();
            watch.Start();
            int added = 0;
            foreach (var doc in docs)
            {
                count++;
                if (count % 100 == 0)
                {
                    Console.WriteLine($"Crawl: {CrawlLocation}: Processed {count} of {docs.Length}. Added to archive: {added}");
                }
                var data = documentStore.GetDocument(doc.UrlID);
                if (Archiver.ArchiveContent(doc.FirstSeen, doc.GeminiUrl, doc.Status ?? 20, doc.Meta, data))
                {
                    added++;
                }
            }
            watch.Stop();
            Console.WriteLine($"Completed processing {CrawlLocation}");
            Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
            Console.WriteLine($"Snapshots Added:\t{added}");
        }
	}
}

