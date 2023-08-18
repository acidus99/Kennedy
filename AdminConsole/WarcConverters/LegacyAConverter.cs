using Gemini.Net;
using Kennedy.Warc;

namespace Kennedy.AdminConsole.WarcConverters
{
    /// <summary>
    /// Converts the legacy TSV with response bodies stored in the directory-based storage system
    /// to WARC records
    /// </summary>
	public class LegacyAConverter : AbstractConverter
    {
        /// <summary>
        /// When this crawl was run
        /// </summary>
        DateTime Captured;

        protected override string ConverterName => "Legacy A";

        public LegacyAConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            : base(warcCreator, crawlLocation)
        {
            CrawlLocation = crawlLocation;

            //The legacy log.tsv format did not store the capture time for individual requests/responses
            //however we can get the time the crawl stated via the filename. That will be stored here
            string recoveredTime = GrabTime(crawlLocation);
            Captured = DateTime.ParseExact(recoveredTime, "yyyy-MM-dd (HHmmss)", null);
        }

        static string GrabTime(string crawlLocation)
        {
            return Path.GetDirectoryName(crawlLocation)!.Split(Path.DirectorySeparatorChar).Reverse().First();
        }

        protected override void ConvertCrawl()
        {
            foreach (var line in File.ReadLines(CrawlLocation + "capture-log.tsv"))
            {
                RecordsProcessed++;
                var fields = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (fields.Length < 4 || !fields[2].StartsWith("20 "))
                {
                    continue;
                }

                GeminiUrl url = new GeminiUrl(fields[1]);
                int statusCode = 20;
                string meta = fields[2].Substring(3); 

                byte[]? data = GetContentData(url);

                string mime = GetJustMimetype(meta);

                bool isTruncated = (data == null);

                WarcCreator.WriteLegacySession(url, Captured, statusCode, meta, mime, data, isTruncated);
                RecordsWritten++;
            }
        }

        private byte[]? GetContentData(GeminiUrl url)
        {
            var path = GetPathForUrl(url);
            return File.ReadAllBytes(path);
        }

        private string GetPathForUrl(GeminiUrl url)
        {
            string filename = (url.Port == 1965) ?
                 $"{url.Hostname}.gmi" :
                 $"{url.Hostname}:{url.Port}.gmi";

            return $"{CrawlLocation}capture/{filename}";
        }
    }
}
