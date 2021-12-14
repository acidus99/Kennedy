using System;
using System.Collections.Generic;
using Gemi.Net;
using GemiCrawler.Utils;

namespace GemiCrawler.MetaStore
{
    /// <summary>
    /// Stores request/response meta data in a log file
    /// </summary>
    public class LogStorage : IMetaStore
    {
        ThreadedFileWriter logOut;

        public LogStorage(string outputDir)
        {
            logOut = new ThreadedFileWriter(outputDir + "log-responses.tsv", 20);
        }

        public void Close()
        {
            logOut.Close();
        }

        public void StoreMetaData(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks)
        {
            var msg = $"{resp.StatusCode}\t{resp.MimeType}\t{url}\t{resp.BodySize}\t{resp.ConnectTime}\t{resp.DownloadTime}\t{foundLinks.Count}";
            logOut.WriteLine(msg);
        }
    }
}
