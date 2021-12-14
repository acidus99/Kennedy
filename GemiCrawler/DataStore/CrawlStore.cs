using System;
using Gemi.Net;
using GemiCrawler.Modules;

namespace GemiCrawler.DataStore
{
    public class CrawlStore : AbstractModule, IDataStore
    {
        string StoragePath;
        CrawlDbContext db;
        object locker;


        public CrawlStore(string storagePath)
            :base("DB-STORE")
        {
            StoragePath = storagePath;
            db = new CrawlDbContext(storagePath);
            locker = new object();
        }

        public bool Store(GemiUrl url, GemiResponse resp)
        {
            
            var storedResp = new StoredResponse
            {
                Url = resp.RequestUrl.NormalizedUrl,
                Domain = resp.RequestUrl.Hostname,
                Port = resp.RequestUrl.Port,

                ConnectStatus = resp.ConnectStatus,
                MetaLine = resp.ResponseLine,
                BodySize = resp.BodySize,
                BodyBytes = resp.BodyBytes,

                ConnectTime = resp.ConnectTime,
                DownloadTime = resp.DownloadTime,
                MimeType = resp.MimeType
            };

            lock (locker)
            {
                db.Responses.Add(storedResp);
                db.SaveChanges();
            }
            return true;
        }

        protected override string GetStatusMesssage()
            => $"Successully Stored: {processedCounter.Count}";
    }
}
