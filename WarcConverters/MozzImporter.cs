namespace Kennedy.WarcConverters;

using System;
using System.IO;
using Gemini.Net;
using Kennedy.WarcConverters.MozzPortalImport;

class MozzImporter
{
    public static readonly DateTime OnlyBefore = new DateTime(2023, 1, 1);

    public static void Import()
    {
        string urlsFile =  ResolveDir("~/tmp/mozz-dump/test-urls.txt");

        ArchivedContentConverter contentConverter = new ArchivedContentConverter();

        StreamWriter logGood = new StreamWriter(File.Create(ResolveDir("~/tmp/mozz-dump/good-results.txt")));
        StreamWriter logBad = new StreamWriter(File.Create(ResolveDir("~/tmp/mozz-dump/bad-resultes.txt")));

        PendingQueue pendingUrls = new PendingQueue();
        //initialize with URLs from input file
        foreach(string url in File.ReadLines(urlsFile).ToArray())
        {
            WaybackUrl waybackUrl = new WaybackUrl(url);
            if(!UrlInScope(waybackUrl))
            {
                continue;
            }

            pendingUrls.Enqueue(waybackUrl);
        }
        int counter = 0;
        while(pendingUrls.Count > 0)
        {
            counter++;
            WaybackUrl waybackUrl = pendingUrls.Dequeue();

            Console.WriteLine($"{pendingUrls.Count}\t{waybackUrl.GetProxiedUrl()}");

            try
            {
                ArchivedContent content = contentConverter.Convert(waybackUrl);
                foreach(var url in content.MoreUrls)
                {
                    if(UrlInScope(url))
                    {
                        pendingUrls.Enqueue(url);
                    }
                }
                string msg = "";

                if (content.GeminiResponse != null)
                {
                    msg = content.GeminiResponse.ToString();
                }
                else if (content.Certificate != null)
                {
                    msg = "Certificate: " + content.Certificate.Subject;
                }
                else
                {
                    msg = "no content could be extracted";
                }

                Console.WriteLine("\t" + msg);
                logGood.WriteLine($"{waybackUrl.Url} {waybackUrl.GetProxiedUrl()} {msg}");
                logGood.Flush();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                logBad.WriteLine($"{waybackUrl.Url} {waybackUrl.GetProxiedUrl()} {ex.Message}");
                logBad.Flush();
            }
            Thread.Sleep(1500);
        }

        logGood.Close();
        logBad.Close();

        return;

    }

    private static bool UrlInScope(WaybackUrl url)
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

        if(url.Captured < OnlyBefore)
        {
            return false;
        }

        return true;
    }

    public static void BuildSnapshotUrls()
    {
        string UrlsFile = ResolveDir("~/tmp/mozz-dump/wayback-capture-urls.txt");

        WaybackClient wbclient = new WaybackClient();

        StreamWriter fout = new StreamWriter(UrlsFile, false);

        var urls = wbclient.GetUrls("https://portal.mozz.us/gemini/");
        var total = urls.Count;
        int curr = 0;
        int collected = 0;
        foreach (var url in urls)
        {
            curr++;
            Console.WriteLine($"{curr} of {total} - Captures: {collected}");

            var captures = wbclient.GetSnapshots(url);

            foreach (var capture in captures)
            {
                collected++;
                fout.WriteLine($"{capture.Timestamp} {capture.OriginalUrl} {capture.ContentType} {capture.CaptureUrl}");
                fout.Flush();
            }
            System.Threading.Thread.Sleep(2000);
        }
        fout.Close();
    }

    private static string ResolveDir(string dir)
        => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
}


