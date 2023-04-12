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

namespace Kennedy.AdminConsole.Importers
{

    //only support documents
    public class DomainCrawlDbContext : DbContext
    {
        protected string StorageDirectory;

        public DbSet<SimpleDomain> Domains { get; set; }
        public DbSet<SimpleDocument> Documents { get; set; }

        public DomainCrawlDbContext(string storageDir)
        {
            StorageDirectory = storageDir;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");
        }
    }

    /// <summary>
    /// Given the crawl-data stored in the modern Kennedy crawl format, import it into the archive
    /// </summary>
	public class DomainImporter
	{
		Archiver Archiver;
		string CrawlLocation;

        DomainCrawlDbContext db;

        public DomainImporter(Archiver archiver, string crawlLocation)
		{
			Archiver = archiver;
			CrawlLocation = crawlLocation;
		}

		public void Import()
		{
            db = new DomainCrawlDbContext(CrawlLocation);
            ImportDomains();
        }

        private void ImportDomains()
        {
            int count = 0;
            var domains = db.Domains
                .Where(x => (x.IsReachable) &&
                    (x.HasFaviconTxt || x.HasRobotsTxt || x.HasSecurityTxt)).ToArray();

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            int added = 0;
            Console.WriteLine($"Adding domains {CrawlLocation}");
            foreach (var domain in domains)
            {
                //find the first datetime for this
                count++;
                Console.WriteLine($"Importing domain {count} - Added: {added}");

                var firstDoc = db.Documents.Where(x => x.Domain == domain.Domain && x.Port == domain.Port).OrderBy(x => x.FirstSeen).FirstOrDefault();

                if (firstDoc == null)
                {
                    continue;
                }

                DateTime captured = firstDoc.FirstSeen;

                if (domain.HasFaviconTxt && !String.IsNullOrEmpty(domain.FaviconTxt))
                {
                    ArchiveSpecialFile(captured, domain, "favicon.txt", domain.FaviconTxt);
                    added++;
                }

                if (domain.HasRobotsTxt && !String.IsNullOrEmpty(domain.RobotsTxt))
                {
                    ArchiveSpecialFile(captured, domain, "robots.txt", domain.RobotsTxt);
                    added++;
                }

                if (domain.HasSecurityTxt && !String.IsNullOrEmpty(domain.SecurityTxt))
                {
                    ArchiveSpecialFile(captured, domain, ".well-known/security.txt", domain.SecurityTxt);
                    added++;
                }
            }
            watch.Stop();
            Console.WriteLine($"Completed processing {CrawlLocation}");
            Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
            Console.WriteLine($"Snapshots Added:\t{added}");
        }

        private void ArchiveSpecialFile(DateTime captured, SimpleDomain domain, string filename, string contents)
        {
            var url = MakeSpecialUrl(domain, filename);
            var data = GetBytes(contents);

            Archiver.ArchiveResponse(captured, url, 20, "text/plain", data, true);
        }

        private GeminiUrl MakeSpecialUrl(SimpleDomain domain, string specialFilename)
            => new GeminiUrl($"gemini://{domain.Domain}:{domain.Port}/{specialFilename}");

        private byte[] GetBytes(string contents)
            => System.Text.Encoding.UTF8.GetBytes(contents);

    }
}

