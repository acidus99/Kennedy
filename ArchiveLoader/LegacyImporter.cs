using System;
using Gemini.Net;
using Kennedy.Archive;
using Kennedy.CrawlData;

namespace ArchiveLoader
{
    /// <summary>
    /// Given the crawl-data stored in the modern Kennedy crawl format, import it into the archive
    /// </summary>
    public class LegacyImporter
	{
        Archiver Archiver;
        string CrawlLocation;

        public LegacyImporter(Archiver archiver, string crawlLocation)
        {
            Archiver = archiver;
            CrawlLocation = crawlLocation;
        }

        public void Import()
        {
            //grab the date of the
            DateTime captured = DateTime.Parse("2021-12-03");

            int success = 0;
            int count = 0;

            foreach(var line in File.ReadLines(CrawlLocation + "log.tsv"))
            {
                count++;
                var fields = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if(fields.Length < 3 || fields[0] != "20")
                {
                    continue;
                }
                int statusCode = Convert.ToInt32(fields[0]);
                string mimeType = fields[1];
                GeminiUrl url = new GeminiUrl(fields[2]);
                byte [] data = GetContentData(url);

                if(data == null)
                {
                    continue;
                }

                Archiver.ArchiveContent(captured, url, statusCode, mimeType, data);

                success++;
            }
            Console.WriteLine($"{CrawlLocation}\t{success} of {count}!");
            int x=4;
        }



        private byte [] GetContentData(GeminiUrl url)
        {
            try
            {
                var path = GetSavePath(url);
                return File.ReadAllBytes(path);
            } catch(Exception)
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

