//using System;
//using System.Net.NetworkInformation;
//using System.Text.Encodings;

//using Gemini.Net;

//using Kennedy.Archive;
//using Kennedy.Data.RobotsTxt;
//using Kennedy.SearchIndex.Models;
//using Kennedy.SearchIndex.Storage;
//using Kennedy.SearchIndex.Web;
//using Microsoft.EntityFrameworkCore;

//namespace Kennedy.AdminConsole.Importers
//{
//    /// <summary>
//    /// Only important interesting response that don't have a body
//    /// </summary>
//	public class MetaResponseImporter
//	{
//		Archiver Archiver;
//		string CrawlLocation;

//        ModernCrawlDbContext db;

//        public MetaResponseImporter(Archiver archiver, string crawlLocation)
//		{
//			Archiver = archiver;
//			CrawlLocation = crawlLocation;
//		}

//		public void Import()
//		{
//            db = new ModernCrawlDbContext(CrawlLocation);
        
//            int count = 0;
//            try
//            {
//                var docs = db.Documents.Where(x => (x.ConnectStatus == ConnectStatus.Success && x.Status != 20)).ToArray();

//                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
//                watch.Start();
//                int added = 0;
//                foreach (var doc in docs)
//                {
//                    count++;
//                    if (count % 100 == 0)
//                    {
//                        Console.WriteLine($"Crawl: {CrawlLocation}: Processed {count} of {docs.Length}. Added to archive: {added}");
//                    }

//                    //we do want to save redirects, status prompts, and auth prompts, since those are interesting
//                    if (GeminiParser.IsInputStatus(doc.Status.Value)
//                        || GeminiParser.IsRedirectStatus(doc.Status.Value)
//                        || GeminiParser.IsAuthStatus(doc.Status.Value))
//                    {
//                        if (Archiver.ArchiveResponse(doc.FirstSeen, doc.GeminiUrl, doc.Status.Value, doc.Meta))
//                        {
//                            added++;
//                        }
//                    }
//                }
//                watch.Stop();
//                Console.WriteLine($"Completed processing {CrawlLocation}");
//                Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
//                Console.WriteLine($"Snapshots Added:\t{added}");
//            }
//            catch (Exception ex)
//            {
//                int x = 4;
//            }
//        }
//    }
//}

