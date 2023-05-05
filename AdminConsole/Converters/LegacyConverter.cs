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
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace Kennedy.AdminConsole.Converters
{
    /// <summary>
    /// Converts the "Documents" table from Kennedy Crawls into WARC files
    /// </summary>
	public class LegacyConverter : AbstractConverter
	{
		string CrawlLocation;
        DateTime Captured;
        
        public LegacyConverter(GeminiWarcCreator warcCreator, string crawlLocation)
            :base(warcCreator)
		{
			CrawlLocation = crawlLocation;
            string recoveredTime = GrabTime(crawlLocation);
            Captured = DateTime.ParseExact(recoveredTime, "yyyy-MM-dd (hhmmss)", null);

        }

        static string GrabTime(string crawlLocation)
        {
            return Path.GetDirectoryName(crawlLocation).Split(Path.DirectorySeparatorChar).Reverse().First();
        }

        public override void ToWarc()
        {

            int warcResponses = 0;
            int warcResponsesTruncated = 0;
            int count = 0;
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            int added = 0;

            foreach (var line in File.ReadLines(CrawlLocation + "log.tsv"))
            {
                count++;
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

                if(isTruncated)
                {
                    warcResponsesTruncated++;
                }
                else
                {
                    warcResponses++;
                }
            }
            watch.Stop();
            Console.WriteLine($"Completed processing {CrawlLocation}");
            Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
            Console.WriteLine($"--");
            Console.WriteLine($"Docs:\t{count}");
            Console.WriteLine($"resps:\t{warcResponses}");
            Console.WriteLine($"Tresps:\t{warcResponsesTruncated}");
            Console.WriteLine($"Added:\t{added}");
        }

        private byte[]? GetContentData(GeminiUrl url)
        {
            try
            {
                var path = GetSavePath(url);
                return File.ReadAllBytes(path);
            }
            catch (Exception)
            { }
            return null;
        }

        private string GetStorageFilename(GeminiUrl url)
        {
            var filename = url.Filename;
            return (filename.Length > 0) ? filename : "index.gmi";
        }

        private string GetSavePath(GeminiUrl url)
        {
            var dir = GetStorageDirectory(url);
            var file = GetStorageFilename(url);
            return dir + file;
        }

        private string GetStorageDirectory(GeminiUrl url)
        {
            string hostDir = (url.Port == 1965) ? url.Hostname : $"{url.Hostname} ({url.Port})";

            string path = Path.GetDirectoryName(url.Path);
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


    }
}

