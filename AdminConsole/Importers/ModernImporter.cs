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
    /// <summary>
    /// Given the crawl-data stored in the modern Kennedy crawl format, import it into the archive
    /// </summary>
	public class ModernImporter
	{
		Archiver Archiver;
		string CrawlLocation;

        ModernCrawlDbContext db;
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
            db = new ModernCrawlDbContext(CrawlLocation);
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

        //private void ImportDomains()
        //{
        //    int count = 0;
        //    var domains = db.Domains
        //        .Where(x => (x.IsReachable) &&
        //            (x.HasFaviconTxt || x.HasRobotsTxt || x.HasSecurityTxt)).ToArray();

        //    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        //    watch.Start();
        //    int added = 0;
        //    Console.WriteLine($"Adding domains {CrawlLocation}");
        //    foreach (var domain in domains)
        //    {
        //        //find the first datetime for this
        //        count++;
        //        Console.WriteLine($"Importing domain {count} - Added: {added}");

        //        var firstDoc = db.Documents.OrderBy(x => x.FirstSeen).FirstOrDefault();

        //        if (firstDoc == null)
        //        {
        //            continue;
        //        }

        //        DateTime captured = firstDoc.FirstSeen;

        //        if (domain.HasFaviconTxt)
        //        {
        //            ArchiveSpecialFile(captured, domain, "favicon.txt", domain.FaviconTxt);
        //            added++;
        //        }

        //        if (domain.HasRobotsTxt)
        //        {
        //            ArchiveSpecialFile(captured, domain, "robots.txt", domain.RobotsTxt);
        //            added++;
        //        }

        //        if (domain.HasSecurityTxt)
        //        {
        //            ArchiveSpecialFile(captured, domain, ".well-known/security.txt", domain.SecurityTxt);
        //            added++;
        //        }
        //    }
        //    watch.Stop();
        //    Console.WriteLine($"Completed processing {CrawlLocation}");
        //    Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
        //    Console.WriteLine($"Snapshots Added:\t{added}");
        //}

        //private void ArchiveSpecialFile(DateTime captured, SimpleDomain domain, string filename, string contents)
        //{
        //    var url = MakeSpecialUrl(domain, filename);
        //    var data = GetBytes(contents);

        //    Archiver.ArchiveResponse(captured, url, 20, "text/plain", data, true);
        //}


        //private GeminiUrl MakeSpecialUrl(SimpleDomain domain, string specialFilename)
        //    => new GeminiUrl($"gemini://{domain.DomainName}:{domain.Port}/{specialFilename}");

        //private byte[] GetBytes(string contents)
        //    => System.Text.Encoding.UTF8.GetBytes(contents);

        //private bool IsAllowedByRobots(GeminiUrl url)
        //{
        //    if (!RobotsCache.ContainsKey(url.Authority))
        //    {
        //        var domainInfo = db.Domains
        //            .Where(x => x.DomainName == url.Hostname && x.Port == url.Port && x.HasRobotsTxt).FirstOrDefault();

        //        RobotsTxtFile robotsFile = null;

        //        if (domainInfo != null)
        //        {
        //            robotsFile = new RobotsTxtFile(domainInfo.RobotsTxt);
        //            //if its malformed or doesn't have archiver rules, we don't care
        //            if (robotsFile.IsMalformed || robotsFile.UserAgents.Contains("archiver"))
        //            {
        //                robotsFile = null;
        //            }
        //        }
        //        RobotsCache[url.Authority] = robotsFile;
        //    }


        //    RobotsTxtFile robots = RobotsCache[url.Authority];
        //    if (robots == null)
        //    {
        //        return true;
        //    }

        //    return robots.IsPathAllowed("archiver", url.Path);
        //}

    }
}

