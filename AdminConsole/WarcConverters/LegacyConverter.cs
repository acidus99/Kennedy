using Gemini.Net;
using Kennedy.Warc;

namespace Kennedy.AdminConsole.WarcConverters
{
    /// <summary>
    /// Converts the legacy TSV with response bodies stored in the directory-based storage system
    /// to WARC records
    /// </summary>
	public class LegacyConverter : AbstractConverter
	{
        /// <summary>
        /// When this crawl was run
        /// </summary>
        DateTime Captured;

        protected override string ConverterName => "Legacy log.tsv + directory-based storage system";

        public LegacyConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            :base(warcCreator, crawlLocation)
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
            foreach (var line in File.ReadLines(CrawlLocation + "log.tsv"))
            {
                RecordsProcessed++;
                var fields = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (fields.Length < 3 || fields[0] != "20")
                {
                    continue;
                }

                GeminiUrl url = new GeminiUrl(fields[2]);
                int statusCode = Convert.ToInt32(fields[0]);
                string meta = fields[1];

                byte[]? data = GetContentData(url);

                string mime = GetJustMimetype(meta);

                bool isTruncated = (data == null);

                WarcCreator.WriteLegacySession(url, Captured, statusCode, meta, mime, data, isTruncated);
                RecordsWritten++;
            }
        }

        private byte[]? GetContentData(GeminiUrl url)
        {
            try
            {
                var path = GetPathForUrl(url);
                return File.ReadAllBytes(path);
            }
            catch (Exception)
            { }
            return null;
        }

        private string GetPathForUrl(GeminiUrl url)
        {
            var dir = GetStorageDirectory(url);
            var file = GetStorageFilename(url);
            return dir + file;
        }

        private string GetStorageDirectory(GeminiUrl url)
        {
            string hostDir = (url.Port == 1965) ? url.Hostname : $"{url.Hostname} ({url.Port})";

            string? path = Path.GetDirectoryName(url.Path);
            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }
            if (!path.EndsWith('/'))
            {
                path += "/";
            }

            return $"{CrawlLocation}page-store/{hostDir}{path}";
        }

        private string GetStorageFilename(GeminiUrl url)
        {
            var filename = url.Filename;
            return (filename.Length > 0) ? filename : "index.gmi";
        }       
    }
}
