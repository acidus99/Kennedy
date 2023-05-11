using System;
using System.Net.NetworkInformation;
using System.Text;

using Gemini.Net;

using Kennedy.Data.RobotsTxt;
using Kennedy.SearchIndex.Models;
using Kennedy.AdminConsole.Storage;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;

using Kennedy.Warc;
using System.IO.Compression;

using Kennedy.AdminConsole.Db;

namespace Kennedy.AdminConsole.WarcConverters
{
    /// <summary>
    /// Converts the Domain table of crawl databases into WARC records and writes them to a file.
    /// Specically this can extract out:
    /// - Robots.txt
    /// - Favicon.txt
    /// - security.txt
    /// </summary>
	public class DomainConverter : AbstractConverter
	{
        DomainDbContext db;

        public DomainConverter(GeminiWarcCreator warcWriter, string crawlLocation)
            : base(warcWriter, crawlLocation)
		{
            db = new DomainDbContext(CrawlLocation);
        }

        protected override string ConverterName => "Domain Table Converter";

        protected override void ConvertCrawl()
        {
            var domains = db.Domains
                .Where(x => (x.IsReachable) &&
                    (x.HasFaviconTxt || x.HasRobotsTxt || x.HasSecurityTxt)).ToArray();

            foreach (var domain in domains)
            {
                RecordsProcessed++;

                DateTime? estimatedCapture = EstimateCaptureTime(domain);
                if (estimatedCapture == null)
                {
                    continue;
                }

                if (domain.HasFaviconTxt && !String.IsNullOrEmpty(domain.FaviconTxt))
                {
                    ConvertSpecialFile(estimatedCapture.Value, domain, "favicon.txt", domain.FaviconTxt);
                    RecordsCreated++;
                }

                if (domain.HasRobotsTxt && !String.IsNullOrEmpty(domain.RobotsTxt))
                {
                    ConvertSpecialFile(estimatedCapture.Value, domain, "robots.txt", domain.RobotsTxt);
                    RecordsCreated++;

                }

                if (domain.HasSecurityTxt && !String.IsNullOrEmpty(domain.SecurityTxt))
                {
                    ConvertSpecialFile(estimatedCapture.Value, domain, ".well-known/security.txt", domain.SecurityTxt);
                    RecordsCreated++;
                }
            }
        }

        private DateTime? EstimateCaptureTime(SimpleDomain domain)
        {
            var firstDoc = db.Documents.Where(x => x.Domain == domain.Domain && x.Port == domain.Port).OrderBy(x => x.FirstSeen).FirstOrDefault();

            return firstDoc?.FirstSeen;
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
