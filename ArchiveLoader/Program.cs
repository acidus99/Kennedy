using System.Linq;

using HashDepot;

using Gemini.Net;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.CrawlData;
using Kennedy.Data;

using Microsoft.EntityFrameworkCore;


namespace ArchiveLoader
{
    class Program
    {
        const string ArchiveLocation = "/tmp/archive.db";
        const string ArchiveStoreRoot = "/tmp/Packs/";

        static void Main(string[] args)
        {
            using(ArchiveDbContext archiveDb = new ArchiveDbContext(ArchiveLocation))
            {
                archiveDb.Database.EnsureCreated();
            }

            foreach (var line in File.ReadAllLines("/Users/billy/Desktop/crawls.txt"))
            {
                LoadCrawlArchive(line);
            }

        }

        static void LoadCrawlArchive(string crawlLocation)
        {
            SimpleDocumentIndexDbContext db = new SimpleDocumentIndexDbContext(crawlLocation);
            DocumentStore documentStore = new DocumentStore(crawlLocation + "page-store/");

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            int count = 0;
            var docs = db.Documents.Where(x => (x.Status == 20 && x.BodySaved)).ToArray();
            watch.Start();
            foreach (var doc in docs) {
                count++;
                if (count % 100 == 0)
                {
                    Console.WriteLine($"Crawl: {crawlLocation}: {count} of {docs.Length}");
                }
                var data = documentStore.GetDocument(doc.UrlID);
                ArchiveEntry(doc, data);
            }
            watch.Stop();
            Console.WriteLine("total Seconds:" + watch.Elapsed.TotalSeconds);
        }

        static void ArchiveEntry(SimpleDocEntry entry, byte [] data)
        {
            ArchiveDbContext db = new ArchiveDbContext(ArchiveLocation);
            var trueID = entry.TrueID();

            PackManager packManager = new PackManager(ArchiveStoreRoot);

            var url = db.Urls.Where(x => x.Id == trueID).FirstOrDefault();
            bool newUrl = false;
            if(url == null)
            {
                url = new Url(new GeminiUrl(entry.Url));
                db.Urls.Add(url);
                newUrl = true;
                //need to save for foreign key constraint
                db.SaveChanges();
            }

            var packFile = packManager.GetPack(url.PackName);

            if (newUrl)
            {
                packFile.Append(PackRecordFactory.MakeInfoRecord(url.FullUrl));
            }

            //OK, create a new snapshot
            var dataHash = GetDataHash(data);

            var previousSnapshot = db.Snapshots
                .Where(x => x.UrlId == url.Id &&
                        x.DataHash == dataHash).FirstOrDefault();

            bool shouldAddSnapshot = true;

            var snapshot = new Snapshot
            {
                Captured = entry.FirstSeen,
                StatusCode = entry.Status ?? 20,
                Size = data.LongLength,
                ContentType = GetContentType(entry),
                Meta = entry.Meta,
                DataHash = dataHash,
                Url = url,
                UrlId = url.Id
            };

            if (previousSnapshot == null)
            {
                //write it into the Pack

                snapshot.Offset = packFile.Append(PackRecordFactory.MakeOptimalRecord(MakePayload(entry, data)));
            }
            else
            {
                //is same as existing
                snapshot.Offset = previousSnapshot.Offset;

                //are the capture times the same? If so, don't save it, because its a dupe
                if (snapshot.Captured == previousSnapshot.Captured)
                {
                    shouldAddSnapshot = false;
                }
            }

            if (shouldAddSnapshot)
            {
                url.Snapshots.Add(snapshot);
                db.Urls.Update(url);
                db.Snapshots.Add(snapshot);
                db.SaveChanges();
            }
        }

        static ContentType GetContentType(SimpleDocEntry entry)
        {
            var status = entry.Status ?? 0;

            if(status == 20)
            {
                if(entry.Meta.StartsWith("text/"))
                {
                    return ContentType.Text;
                } else if(entry.Meta.StartsWith("image/"))
                {
                    return ContentType.Image;
                }
                return ContentType.Binary;
            }
            return ContentType.Unknown;
        }

        static long GetDataHash(byte [] body)
        {
            //want signed long, so use 32 bit hash.
            return Convert.ToInt64(XXHash.Hash32(body));
        }

        static byte [] MakePayload(SimpleDocEntry entry, byte [] body)
        {
            //string requestLine = $"{entry.Status} {entry.Meta}\r\n";
            //List<byte> buffer = new List<byte>();
            //buffer.AddRange(System.Text.Encoding.UTF8.GetBytes(requestLine));
            //buffer.AddRange(body);
            //return buffer.ToArray();
            return body;
        }
    }
}
