using System;
using System.Net.NetworkInformation;
using System.Text.Encodings;

using Gemini.Net;

using Kennedy.Archive;
using Kennedy.Data.RobotsTxt;
using Kennedy.SearchIndex.Models;
using Kennedy.AdminConsole.Storage;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;

using Kennedy.AdminConsole.Db;
using Kennedy.Warc;
using Warc;
using System.Reflection.Metadata;

namespace Kennedy.AdminConsole.WarcConverters
{
    /// <summary>
    /// Converts the "Documents" table from Kennedy Crawls into WARC files with a
    /// backing "page-store"
    /// </summary>
	public class DocumentConverter : AbstractConverter
    {
        DocumentDbContext db;
        IDocumentStore documentStore;

        public DocumentConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            :base(warcCreator, crawlLocation)
		{
			CrawlLocation = crawlLocation;
            documentStore = new DocumentStore(crawlLocation + "page-store/");
            db = new DocumentDbContext(CrawlLocation);
        }

        protected override string ConverterName => "Documents Table with backing Page Store";

        protected override void ConvertCrawl()
        {
            foreach (var doc in db.Documents)
            {
                RecordsProcessed++;
                try
                {
                    //some older crawls had bad URLs and ports in them
                    //e.g.: gemini://300:de6b:9ffb:a959::d:1965/
                    //so force creating a GeminiUrl and move on if any exceptions happen
                    var dummy = doc.GeminiUrl;
                }
                catch (Exception)
                {
                    continue;
                }

                ///====== Normalize the data

                //older crawls had a status of 0 if there was a connection error, so normalize that to our code 49
                if (doc.Status == 0)
                {
                    doc.Status = GeminiParser.ConnectionErrorStatusCode;
                }

                //if the response is too large, we need to salage the original mime type back into the meta
                if (GeminiParser.IsSuccessStatus(doc.Status) && doc.Meta.StartsWith("Requestor aborting due to reaching max download"))
                {
                    doc.Meta = doc.MimeType;
                }
                //if its another type of error while downloading, the status should be our generic errors
                else if (GeminiParser.IsSuccessStatus(doc.Status) & doc.ConnectStatus == ConnectStatus.Error && !doc.Meta.StartsWith("Requestor aborting due to reaching max download"))
                {
                    //just a regular error
                    doc.Status = GeminiParser.ConnectionErrorStatusCode;
                }

                //clean up the Mime to just be content-type
                if (GeminiParser.IsSuccessStatus(doc.Status))
                {
                    doc.MimeType = GetJustMimetype(doc.MimeType);
                }
                else
                {
                    //only successfully responses can have mimetypes
                    doc.MimeType = "";

                    //some error messages had leading/trailing spaces. fix that
                    doc.Meta = doc.Meta.Trim();
                }

                bool isTruncated = IsTruncated(doc); ;
                byte[]? bodyBytes = doc.BodySaved ?
                    documentStore.GetDocument(doc.UrlID) :
                    null;

                if (GeminiParser.IsSuccessStatus(doc.Status) && bodyBytes == null)
                {
                    isTruncated = true;
                }
                
                WarcCreator.WriteLegacySession(doc.GeminiUrl, doc.FirstSeen, doc.Status, doc.Meta, doc.MimeType, bodyBytes, isTruncated);
                RecordsWritten++;
            }
        }
    }
}
