using Gemi.Net;
using GemiCrawler.DocumentIndex.Db;
using System.Collections.Generic;

namespace GemiCrawler.DocumentIndex
{
    public class DocIndex : IMetaStore
    {
        string StoragePath;
        DocIndexDbContext db;
        object locker;


        public DocIndex(string storagePath)
        {
            StoragePath = storagePath;
            db = new DocIndexDbContext(storagePath);
            locker = new object();
        }

        public void Close()
        {
            //nop
        }

        public void StoreMetaData(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks, string storageKey)
        {
            var storedResp = new StoredDocEntry
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
