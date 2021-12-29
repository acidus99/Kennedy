using System;
using GemiCrawler.Utils;
using Gemi.Net;
using System.Collections.Generic;
using GemiCrawler.DocumentIndex.Db;
using System.Linq;

namespace GemiCrawler.DocumentIndex
{

    /// <summary>
    /// Stores how pages link together
    /// </summary>
    public class LinkStorage
    {
        string StoragePath;
        LinkIndexDbContext db;

        public LinkStorage(string storagePath)
        {
            StoragePath = storagePath;
            //create and distory a DBContext to force the DB to be there
            var db = new LinkIndexDbContext(storagePath);
            
        }

        public void Close()
        {
            //nop since we are not caching any writes
        }

        private long toLong(ulong ulongValue)
            => unchecked((long)ulongValue);

        private ulong toULong(long longValue)
            => unchecked((ulong)longValue);

        public void StoreLinks(GemiUrl sourcePage, List<GemiUrl> links)
        {

            using (var db = new LinkIndexDbContext(StoragePath))
            {
                db.LinkEntries.AddRange(links.Distinct().Select(target => new StoredLinkEntry
                {
                    DBSourceDocID = toLong(sourcePage.DocID),
                    DBTargetDocID = toLong(target.DocID),
                    LinkText = "Some magic text!"
                }));

                db.SaveChanges();
            }
        }
    }
}
