using System.Text;
using Gemini.Net;
using Kennedy.AdminConsole.Db;
using Kennedy.AdminConsole.Storage;
using Kennedy.Warc;

namespace Kennedy.AdminConsole.WarcConverters
{
    /// <summary>
    /// Converts the "Documents" table from Kennedy Crawls into WARC files with a
    /// backing "page-store"
    /// </summary>
	public class CrawlDbConverter: AbstractConverter
    {
        DocumentDbContext? docDB = null;
        DomainDbContext? domainDB = null;
        CrawlDbDocumentStore documentStore;

        public CrawlDbConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            : base(warcCreator, crawlLocation)
        {
            documentStore = new CrawlDbDocumentStore(crawlLocation + "page-store/");

            try
            {
                docDB = new DocumentDbContext(crawlLocation);
                docDB.Documents.FirstOrDefault();
            }
            catch (Exception)
            {
                docDB = null;
            }

            try
            {
                domainDB = new DomainDbContext(crawlLocation);
                domainDB.Domains.FirstOrDefault();
            }
            catch (Exception)
            {
                domainDB = null;
            }
        }

        protected override string ConverterName => "New Converter";

        protected override void ConvertCrawl()
        {
            ConvertDocuments();
            ConvertDomains();
        }

        private void ConvertDocuments()
        {
            if(docDB == null)
            {
                return;
            }

            foreach (var doc in docDB.Documents)
            {
                RecordsProcessed++;

                //===== Validation

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

                if(GeminiParser.IsSuccessStatus(doc.Status) && doc.Meta.Contains('\n'))
                {
                    //malformed meta, just skip it
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
                    // Is success means there is a mimetype.
                    doc.Meta = doc.MimeType!;
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
                    // Is success means there is a mimetype.
                    doc.MimeType = GetJustMimetype(doc.MimeType!);
                }
                else
                {
                    //only successfully responses can have mimetypes
                    doc.MimeType = "";

                    //some error messages had leading/trailing spaces. fix that
                    doc.Meta = doc.Meta.Trim();
                }

                bool isTruncated = IsTruncated(doc); ;
                byte[]? bodyBytes = GetBodyBytes(doc.UrlID);

                if (GeminiParser.IsSuccessStatus(doc.Status) && bodyBytes == null)
                {
                    isTruncated = true;
                }

                WarcCreator.WriteLegacySession(doc.GeminiUrl, doc.FirstSeen, doc.Status, doc.Meta, doc.MimeType, bodyBytes, isTruncated);
                RecordsWritten++;
            }
        }

        private byte[]? GetBodyBytes(long urlID)
        {
            try
            {
                if (documentStore.Exists)
                {
                    return documentStore.GetDocument(urlID);
                }
            }
            catch(Exception ex)
            {
            }
            return null;
        }

        private void ConvertDomains()
        {
            if(domainDB == null)
            {
                return;
            }

            var domains = domainDB.Domains
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
                    RecordsWritten++;
                }

                if (domain.HasRobotsTxt && !String.IsNullOrEmpty(domain.RobotsTxt))
                {
                    ConvertSpecialFile(estimatedCapture.Value, domain, "robots.txt", domain.RobotsTxt);
                    RecordsWritten++;

                }

                if (domain.HasSecurityTxt && !String.IsNullOrEmpty(domain.SecurityTxt))
                {
                    ConvertSpecialFile(estimatedCapture.Value, domain, ".well-known/security.txt", domain.SecurityTxt);
                    RecordsWritten++;
                }
            }
        }

        private DateTime? EstimateCaptureTime(SimpleDomain domain)
        {
            var firstDoc = domainDB!.Documents.Where(x => x.Domain == domain.Domain && x.Port == domain.Port).OrderBy(x => x.FirstSeen).FirstOrDefault();

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
