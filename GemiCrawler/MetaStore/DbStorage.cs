using Gemi.Net;
using GemiCrawler.MetaStore.Db;
using System.Collections.Generic;

namespace GemiCrawler.MetaStore
{
    public class DbStorage : IMetaStore
    {
        string StoragePath;
        CrawlDbContext db;
        object locker;


        public DbStorage(string storagePath)
        {
            StoragePath = storagePath;
            db = new CrawlDbContext(storagePath);
            locker = new object();
        }

        public void Close()
        {
            //nop
        }

        public void StoreMetaData(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks)
        {
            var storedResp = new StoredResponse
            {
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

            lock (locker)
            {
                db.Responses.Add(storedResp);
                db.SaveChanges();
            }
        }
    }
}
