using System;
using System.IO;
using System.Threading.Tasks;
using Gemi.Net;
using GemiCrawler.Utils;
using GemiCrawler;
using GemiCrawler.DocumentIndex.Db;
using GemiCrawler.DocumentStore;
using System.Collections.Generic;
using GemiCrawler.Utils;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace GemiCrawler.Support
{
    /// <summary>
    /// Tool to rebuild the pending URL Frontier from the crawl results
    /// </summary>
    public static class RebuildFrontier
    {
        public static void DoIt()
        {
            ThreadSafeCounter counter = new ThreadSafeCounter();


            var db = new DocIndexDbContext(Crawler.DataDirectory);
            var urlsInDocIndex = (db.DocEntries
                        .Where(x => x.BodySize > 0).ToList()
                         .Select(x => toULong(x.DBDocID)));

            Bag<GemiUrl> unvisitedUrls = new Bag<GemiUrl>();

            //grab the URL and DocID for all documents which have links to targets that
            //don't exist in the database. These are the pages we need to parge to find the
            //unvisited links

            var comparer = new StoredEntryComparer();


            var docsWithUnvisitedLinks = db.DocEntries.FromSqlRaw
                (@"select * from Documents
                    Inner join Links
                    on DBDocID = DBSourceDocID
                    Where BodySize > 0 and Links.DBTargetDocID not in (Select Documents.DBDocID from Documents)").ToList().Distinct(comparer).ToList();


            int total = docsWithUnvisitedLinks.Count;

            Parallel.ForEach(docsWithUnvisitedLinks, new ParallelOptions { MaxDegreeOfParallelism = 8 }, entry =>
            {

                var docStore = new DocStore(@"/Users/billy/Code/gemini-play/crawl-out/2021-12-31 (024040)/page-store");
                string bodyText = System.Text.Encoding.UTF8.GetString(docStore.GetDocument(toULong(entry.DBDocID)));

                var foundLinks = GemiCrawler.GemText.LinkFinder.ExtractBodyLinks(new GemiUrl(entry.Url), bodyText);

                foreach (var link in foundLinks)
                {
                    if (!urlsInDocIndex.Contains(link.Url.DocID))
                    {
                        unvisitedUrls.Add(link.Url);
                    }
                }

                int x = counter.Increment();
                Console.WriteLine($"{x}\t{total}");

            }); //close method invocation

            int x = 5;

            File.WriteAllLines($"{Crawler.DataDirectory}rebuilt.txt", unvisitedUrls.GetValues().Select(x => x.NormalizedUrl));

        }

        public static ulong toULong(long longValue)
            => unchecked((ulong)longValue);


        private class StoredEntryComparer : IEqualityComparer<StoredDocEntry>
        {
            public bool Equals(StoredDocEntry g1, StoredDocEntry g2)
            {
                return g1.DBDocID == g2.DBDocID;
            }

            public int GetHashCode([DisallowNull] StoredDocEntry obj)
            {
                return obj.DBDocID.GetHashCode();
            }
        }

    }
}
