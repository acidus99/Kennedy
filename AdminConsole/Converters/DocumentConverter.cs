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

                    bool isTruncated = false;

                    byte[] data = null;
                    //older crawls didn't explicitly set a status code for connection errors, so do that now
                    if(doc.ConnectStatus == ConnectStatus.Error && doc.Status != 20) 
                    {
                        doc.Status = GeminiParser.ConnectionErrorStatusCode;
                    }

                    if (doc.BodySaved)
                    {
                        data = documentStore.GetDocument(doc.UrlID);
                    }

                    //we don't actually have responseReceived times, so approximate them

                    if (DownloadWasSkipped(doc))
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

