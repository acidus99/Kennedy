namespace Kennedy.WarcConverters;

using System;
using System.IO;
using Gemini.Net;
using Kennedy.WarcConverters.MozzPortalImport;

class MozzImporter
{
    static void Import(string[] args)
    {
        string urlsFile =  ResolveDir("~/tmp/mozz-dump/test-urls.txt");

        ArchivedContentConverter contentConverter = new ArchivedContentConverter();

        StreamWriter logGood = new StreamWriter(File.Create(ResolveDir("~/tmp/mozz-dump/good-results.txt")));
        StreamWriter logBad = new StreamWriter(File.Create(ResolveDir("~/tmp/mozz-dump/bad-resultes.txt")));

        var lines = File.ReadLines(urlsFile);
        int counter = 0;
        int max = lines.Count();
        foreach (string url in lines)
        {
            counter++;

            WaybackUrl waybackUrl = new WaybackUrl(url);

            if (!waybackUrl.IsMozzUrl)
            {
                throw new ApplicationException("Working on Wayback URL that is not for mozz proxy!");
            }

            Console.WriteLine($"{counter} of {max}\t{waybackUrl.GetProxiedUrl()}");

            try
            {
                ArchivedContent content = contentConverter.Convert(waybackUrl);
                string msg = "";

                if (content.GeminiResponse != null)
                {
                    msg = content.GeminiResponse.ToString();
                }
                else if (content.Certificate != null)
                {
                    msg = "Certificate: " + content.Certificate.Subject;
                }
                Console.WriteLine(msg);
                logGood.WriteLine($"{url} {waybackUrl.GetProxiedUrl()} {msg}");
                logGood.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                logBad.WriteLine($"{url} {waybackUrl.GetProxiedUrl()} {ex.Message}");
                logBad.Flush();
            }
            Thread.Sleep(1500);
        }

        logGood.Close();
        logBad.Close();

        return;

    }

    //static void BuildSnapshotUrls()
    //{
    //    string urls = ResolveDir("~/tmp/NEW-mozz-capture-urls.txt");

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
    //        System.Threading.Thread.Sleep(1500);
    //    }
    //    fout.Close();
    //}

    private static string ResolveDir(string dir)
        => dir.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + '/');
}


