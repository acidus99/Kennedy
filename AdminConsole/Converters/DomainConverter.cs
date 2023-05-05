using System;
using System.Net.NetworkInformation;
using System.Text;

using Gemini.Net;

using Kennedy.Data.RobotsTxt;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Storage;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;

using Kennedy.Warc;
using System.IO.Compression;

using Kennedy.AdminConsole.Db;

namespace Kennedy.AdminConsole.Converters
{
    //only support documents

    /// <summary>
    /// Given the crawl-data stored in the modern Kennedy crawl format, import it into the archive
    /// </summary>
	public class DomainConverter : AbstractConverter
	{
		string CrawlLocation;

        DomainDbContext db;

        public DomainConverter(GeminiWarcCreator warcWriter, string crawlLocation)
            : base(warcWriter)
		{
			CrawlLocation = crawlLocation;
            db = new DomainDbContext(CrawlLocation);
        }

		public override void ToWarc()
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
                //Console.WriteLine($"Importing domain {count} - Added: {added}");

                var firstDoc = db.Documents.Where(x => x.Domain == domain.Domain && x.Port == domain.Port).OrderBy(x => x.FirstSeen).FirstOrDefault();

                if (firstDoc == null)
                {
                    continue;
                }

                DateTime captured = firstDoc.FirstSeen;

                if (domain.HasFaviconTxt && !String.IsNullOrEmpty(domain.FaviconTxt))
                {
                    ConvertSpecialFile(captured, domain, "favicon.txt", domain.FaviconTxt);
                    added++;
                }

                if (domain.HasRobotsTxt && !String.IsNullOrEmpty(domain.RobotsTxt))
                {
                    ConvertSpecialFile(captured, domain, "robots.txt", domain.RobotsTxt);
                    added++;
                }

                if (domain.HasSecurityTxt && !String.IsNullOrEmpty(domain.SecurityTxt))
                {
                    ConvertSpecialFile(captured, domain, ".well-known/security.txt", domain.SecurityTxt);
                    added++;
                }
            }
            watch.Stop();
            Console.WriteLine($"Completed DOMAIN processing {CrawlLocation}");
            Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
            Console.WriteLine($"Snapshots Added:\t{added}");
        }

        private void ConvertSpecialFile(DateTime captured, SimpleDomain domain, string filename, string contents)
        {
            var url = MakeSpecialUrl(domain, filename);
            WarcCreator.WriteLegacySession(url, captured, 20, "text/plain", "text/plain", Encoding.UTF8.GetBytes(contents), false);
        }

        private GeminiUrl MakeSpecialUrl(SimpleDomain domain, string specialFilename)
            => new GeminiUrl($"gemini://{domain.Domain}:{domain.Port}/{specialFilename}");

      
    }
}

