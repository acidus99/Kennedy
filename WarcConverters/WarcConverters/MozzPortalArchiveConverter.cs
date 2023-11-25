namespace Kennedy.WarcConverters;

using Gemini.Net;
using Kennedy.Warc;
using Kennedy.WarcConverters.MozzPortalImport;

/// <summary>
/// Converts the legacy-A crawl format into a WARC file
/// </summary>
public class MozzPortalArchiveConverter : AbstractConverter
{
    public static readonly DateTime OnlyBefore = new DateTime(2023, 1, 1);

    protected override string ConverterName => "Mozz Portal Archive";

    PendingQueue pendingUrls = new PendingQueue();

    public MozzPortalArchiveConverter(GeminiWarcCreator warcCreator, string urlsFile)
        : base(warcCreator, urlsFile)
    {
    }

    private void LoadInitialUrls(string urlsFile)
    {
        //initialize with URLs from input file
        foreach (string url in File.ReadLines(urlsFile).ToArray())
        {
            WaybackUrl waybackUrl = new WaybackUrl(url);
            if (!UrlInScope(waybackUrl))
            {
                continue;
            }
            pendingUrls.Enqueue(waybackUrl);
        }
    }

    private void AddUrls(List<WaybackUrl> urls)
    {
        foreach (var url in urls)
        {
            if (UrlInScope(url))
            {
                pendingUrls.Enqueue(url);
            }
        }
    }

    private bool UrlInScope(WaybackUrl url)
    {
        //not long enought to be valid
        if (url.Url.OriginalString.Length < 79)
        {
            return false;
        }

        if (!url.IsMozzUrl)
        {
            return false;
        }

        return (url.Captured < OnlyBefore);
    }

    protected override void ConvertCrawl()
    {
        LoadInitialUrls(CrawlLocation);

        ArchivedContentConverter contentConverter = new ArchivedContentConverter();

        while (pendingUrls.Count > 0)
        {
            RecordsProcessed++;
            WaybackUrl waybackUrl = pendingUrls.Dequeue();
            Console.WriteLine($"{RecordsProcessed}\t{waybackUrl.Captured}\t{waybackUrl.GetProxiedUrl()}");
            try
            {
                ArchivedContent content = contentConverter.Convert(waybackUrl);
                AddUrls(content.MoreUrls);
                WriteContentToWarc(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Thread.Sleep(1500);
        }
    }

    private void WriteContentToWarc(ArchivedContent content)
    {

        DateTime captured = content.Url.Captured;
        GeminiUrl geminiUrl = content.Url.GetProxiedUrl();

        if(content.Certificate != null)
        {
            WarcCreator.WriteLegacyCertificate(captured, geminiUrl, content.Certificate);
        }

        if(content.GeminiResponse != null)
        {
            WarcCreator.WriteSession(content.GeminiResponse);
        }
    }

    // LEGACY! Used to get the list of URLS for all content Mozz portal captures in the internet archive 
    //public static void BuildSnapshotUrls()
    //{
    //    string UrlsFile = ResolveDir("~/tmp/mozz-dump/wayback-capture-urls.txt");

    //    WaybackClient wbclient = new WaybackClient();

    //    StreamWriter fout = new StreamWriter(UrlsFile, false);

    //    var urls = wbclient.GetUrls("https://portal.mozz.us/gemini/");
    //    var total = urls.Count;
    //    int curr = 0;
    //    int collected = 0;
    //    foreach (var url in urls)
    //    {
    //        curr++;
    //        Console.WriteLine($"{curr} of {total} - Captures: {collected}");

    //        var captures = wbclient.GetSnapshots(url);

    //        foreach (var capture in captures)
    //        {
    //            collected++;
    //            fout.WriteLine($"{capture.Timestamp} {capture.OriginalUrl} {capture.ContentType} {capture.CaptureUrl}");
    //            fout.Flush();
    //        }
    //        System.Threading.Thread.Sleep(2000);
    //    }
    //    fout.Close();
    //}

}
