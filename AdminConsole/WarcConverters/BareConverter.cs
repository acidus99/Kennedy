using Gemini.Net;
using Kennedy.AdminConsole.Db;
using Kennedy.Warc;

namespace Kennedy.AdminConsole.WarcConverters
{
    /// <summary>
    /// Converts the "Documents" table from a bare scan database.
    /// While there is text/content in the FTS tables of the Sqlite database
    /// this has been filtered/edited (link URLs removed, preformatted text stripped)
    /// so we cannot use it to accurately reconstruct the content for a URL.
    ///
    /// The only thing we can reconstruct accurately are:
    /// - any/all responses without a body (redirects, input prompts, error messages)
    /// - all 20 responses can be truncated responses with the correct mimetype
    /// 
    /// </summary> 
	public class BareConverter : AbstractConverter
    {
        DocumentDbContext db;

        public BareConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            : base(warcCreator, crawlLocation)
        {
            db = new DocumentDbContext(CrawlLocation);
        }

        protected override string ConverterName => "Domains Table (no backing store)";

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

                //since this is a bare crawl database without a backing store, we have no respones bodies.
                //anything that is successful is truncated, everything else is not

                bool isTruncated = GeminiParser.IsSuccessStatus(doc.Status);

                WarcCreator.WriteLegacySession(doc.GeminiUrl, doc.FirstSeen, doc.Status, doc.Meta, doc.MimeType, null, isTruncated);
                RecordsWritten++;
            }
        }
    }
}
