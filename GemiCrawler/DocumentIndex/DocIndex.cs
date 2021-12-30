using System.Data;
using Gemi.Net;
using GemiCrawler.DocumentIndex.Db;
using System.Collections.Generic;
using EFCore.BulkExtensions;

namespace GemiCrawler.DocumentIndex
{
    public class DocIndex
    {
        string StoragePath;

        public DocIndex(string storagePath)
        {
            StoragePath = storagePath;
            //create and distory a DBContext to force the DB to be there
            var db = new DocIndexDbContext(storagePath);
            db.Database.EnsureCreated();
        }

        public void Close()
        {
            ///nop since we are not caching any writes
        }

        private long toLong(ulong ulongValue)
            => unchecked((long)ulongValue);

        private ulong toULong(long longValue)
            => unchecked((ulong)longValue);

        public void StoreMetaData(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks)
        {
            var entry = new StoredDocEntry
            {
                DBDocID = toLong(url.DocID),
                FirstSeen = System.DateTime.Now,
                LastVisit = System.DateTime.Now,

                BodyHash = resp.BodyHash,

                Url = resp.RequestUrl.NormalizedUrl,
                Domain = resp.RequestUrl.Hostname,
                Port = resp.RequestUrl.Port,

                ConnectStatus = resp.ConnectStatus,
                MetaLine = resp.ResponseLine,
                BodySize = resp.BodySize,

                ConnectTime = resp.ConnectTime,
                DownloadTime = resp.DownloadTime,
                MimeType = resp.MimeType
            };

            using (var db = new DocIndexDbContext(StoragePath))
            {
                db.DocEntries.Add(entry);
                db.SaveChanges();
            }
        }

        public void StoreLinks(GemiUrl sourcePage, List<GemiUrl> links)
        {
            using (var db = new DocIndexDbContext(StoragePath))
            {
                db.BulkInsert(links.Distinct().Select(target => new StoredLinkEntry
                {
                    SourceURL = sourcePage.NormalizedUrl,
                    TargetURL = target.NormalizedUrl,
                    DBSourceDocID = toLong(sourcePage.DocID),
                    DBTargetDocID = toLong(target.DocID),
                    LinkText = "Some magic text!"
                }).ToList());

                db.SaveChanges();
                //7102968342834367033
            }
        }


    }
}
