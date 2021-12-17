using System;
using GemiCrawler.Utils;
using Gemi.Net;
using System.Collections.Generic;

namespace GemiCrawler.MetaStore
{

    /// <summary>
    /// Stores how pages link together
    /// </summary>
    public class LinkStorage
    {
        ThreadedFileWriter logOut;

        public LinkStorage(string outputDir)
        {
            logOut = new ThreadedFileWriter(outputDir + "log-links.tsv", 20);
        }

        public void Close()
        {
            logOut.Close();
        }

        public void StoreMetaData(GemiUrl sourcePage, List<GemiUrl> links)
        {
            foreach(var link in links)
            {
                logOut.WriteLine($"{sourcePage.NormalizedUrl}\t{link.NormalizedUrl}");
            }
        }
    }
}
