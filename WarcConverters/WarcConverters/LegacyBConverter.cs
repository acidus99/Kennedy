namespace Kennedy.WarcConverters;

using Gemini.Net;
using Kennedy.Warc;

/// <summary>
/// Converts the legacy-B crawl format into a WARC file
/// </summary>
public class LegacyBConverter : AbstractConverter
{
    /// <summary>
    /// When this crawl was run
    /// </summary>
    DateTime Captured;

    protected override string ConverterName => "Legacy-B";

    public LegacyBConverter(GeminiWarcCreator warcCreator, string crawlLocation)
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
            var fields = line.Split('\t', StringSplitOptions.TrimEntries);
            if (fields.Length < 3 || fields[0] != "20")
            {
                continue;
            }

            GeminiUrl url = new GeminiUrl(fields[2]);
            int statusCode = Convert.ToInt32(fields[0]);
            //its OK to have an empty meta, we will figure out MIME type later
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

        string path;
        try
        {
            path = GetPathForUrl(url);
            return File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            int xxx = 4;

        }
        return null;
    }

    private string GetPathForUrl(GeminiUrl url)
    {
        var dir = GetStorageDirectory(url);
        var file = GetStorageFilename(url);

        var path = dir + file;


        //sanity check, are we trying to access a directory?
        //then adjust it to be the index
        if(Directory.Exists(path))
        {
            if (!path.EndsWith('/'))
            {
                path += "/";
            }
            path += "index.gmi";
        }

        return path;
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
