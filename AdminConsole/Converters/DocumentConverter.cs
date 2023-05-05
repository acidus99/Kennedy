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
using Warc;
using System.Reflection.Metadata;

namespace Kennedy.AdminConsole.Converters
{
    /// <summary>
    /// Converts the "Documents" table from Kennedy Crawls into WARC files
    /// </summary>
	public class DocumentConverter : AbstractConverter
    {
		string CrawlLocation;

        DocumentDbContext db;
        IDocumentStore documentStore;

        public DocumentConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            :base(warcCreator)
		{
			CrawlLocation = crawlLocation;
            documentStore = new DocumentStore(crawlLocation + "page-store/");
		}

        public override void ToWarc()
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

                    bool isTruncated = IsTruncated(doc); ;
                    byte[]? bodyBytes = doc.BodySaved ?
                        documentStore.GetDocument(doc.UrlID) :
                        null;

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

