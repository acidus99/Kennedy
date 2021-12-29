using System.Data;
using Gemi.Net;
using GemiCrawler.DocumentIndex.Db;
using System.Collections.Generic;

namespace GemiCrawler.DocumentIndex
{
    public class DocIndex : IMetaStore
    {
        string StoragePath;

        public DocIndex(string storagePath)
        {
            StoragePath = storagePath;
            //create and distory a DBContext to force the DB to be there
            var db = new DocIndexDbContext(storagePath);
        }

        public void Close()
        {
            ///nop since we are not caching any writes
        }

        private long toLong(ulong ulongValue)
            => unchecked((long)ulongValue);

        private ulong toULong(long longValue)
            => unchecked((ulong)longValue);

        public void StoreMetaData(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks, string storageKey)
        {
            var entry = new StoredDocEntry
            {
                DBDocID = toLong(url.DocID),
                FirstSeen = System.DateTime.Now,

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
    }
}
