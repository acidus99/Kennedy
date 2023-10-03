using Gemini.Net;
using Kennedy.Warc;

using Kennedy.WarcConverters.Storage;

namespace Kennedy.WarcConverters.WarcConverters
{
    /// <summary>
    /// Converts the legacy-C crawl format into a WARC file
    /// </summary>
	public class LegacyCConverter : AbstractConverter
	{
        /// <summary>
        /// When this crawl was run
        /// </summary>
        DateTime Captured;

        ObjectStore objectStore;

        protected override string ConverterName => "Legacy-C";

        public LegacyCConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            :base(warcCreator, crawlLocation)
		{
            objectStore = new ObjectStore(crawlLocation + "page-store/");

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
            foreach (var line in File.ReadLines(CrawlLocation + "log-responses.tsv"))
            {
                RecordsProcessed++;
                var fields = line.Split('\t', StringSplitOptions.TrimEntries);
                if (fields.Length < 3 || fields[0] != "20")
                {
                    continue;
                }

                GeminiUrl url = new GeminiUrl(fields[2]);
                int statusCode = Convert.ToInt32(fields[0]);
                string meta = fields[1];

                string hash = (fields.Length == 9) ? fields[8] : "";

                byte[]? data = GetContentData(hash);

                string mime = GetJustMimetype(meta);

                bool isTruncated = (data == null);

                WarcCreator.WriteLegacySession(url, Captured, statusCode, meta, mime, data, isTruncated);
                RecordsWritten++;
            }
        }

        private byte[]? GetContentData(string? hash)
        {
            if(string.IsNullOrEmpty(hash))
            {
                return null;
            }

            return objectStore.GetObject(hash);
        }
    }
}
