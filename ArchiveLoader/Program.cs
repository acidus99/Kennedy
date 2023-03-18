using System.Linq;

using HashDepot;

using Gemini.Net;

using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.CrawlData;
using Kennedy.Data;



namespace ArchiveLoader
{
    class Program
    {
        const string ArchiveLocation = "/Users/billy/Desktop/archive.db";
        const string ArchiveStoreRoot = "/Users/billy/Desktop/Packs/";

        static void Main(string[] args)
        {
            var crawlLocation = "/Users/billy/Desktop/ARCHIVE PROJECT/Sorted/2022-01-09/";

            using(ArchiveDbContext archiveDb = new ArchiveDbContext(ArchiveLocation))
            {
                archiveDb.Database.EnsureCreated();
            }

            SimpleDocumentIndexDbContext db = new SimpleDocumentIndexDbContext(crawlLocation);
            DocumentStore documentStore = new DocumentStore(crawlLocation + "page-store/");

            Console.WriteLine("Kennedy Archive Loader!");
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            int count = 0;
            var docs = db.Documents.Where(x => (x.Status == 20 && x.BodySaved)).Take(3000).ToArray();
            watch.Start();
            Parallel.ForEach(docs, new ParallelOptions { MaxDegreeOfParallelism = 1 }, doc =>
            {
                count++;
                if (count % 100 == 0)
                {
                    Console.WriteLine($"{count} of {docs.Length}");
                }
                var data = documentStore.GetDocument(doc.DocID);
                ArchiveEntry(doc, data);

            }); //close method invocation 

            watch.Stop();
            int xxxx = 5;
            Console.WriteLine("total Seconds:" + watch.Elapsed.TotalSeconds);

        }

        static void ArchiveEntry(SimpleDocEntry entry, byte [] data)
        {
            ArchiveDbContext db = new ArchiveDbContext(ArchiveLocation);
            var urlEntry = db.Urls.Where(x => x.UrlId == entry.DBDocID).FirstOrDefault();

            if(urlEntry == null)
            {
                urlEntry = new UrlEntry(new GeminiUrl(entry.Url));
                db.Urls.Add(urlEntry);
                //need to save for foreign key constraint
                //db.SaveChanges();
            }

            //OK, create a new snapshot

            SnapshotEntry snapshot = new SnapshotEntry
            {
                Captured = entry.FirstSeen,
                StatusCode = entry.Status ?? 20,
                Size = data.LongLength,
                ContentType = GetContentType(entry),
                Meta = entry.Meta,
                DataHash = GetDataHash(data),
                UrlEntry = urlEntry,
                UrlId = urlEntry.UrlId
            };

            //write it into the Pack
            PackManager packManager = new PackManager(ArchiveStoreRoot);
            var packFile = packManager.GetPack(urlEntry.PackName);

            snapshot.Offset = packFile.Append(PackRecordFactory.MakeOptimalRecord(MakePayload(entry, data)));

            //urlEntry.Snapshots.Add(snapshot);
            //db.Urls.Update(urlEntry);
            db.Snapshots.Add(snapshot);

            db.SaveChanges();
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
            string requestLine = $"{entry.Status} {entry.Meta}\r\n";
            List<byte> buffer = new List<byte>();
            buffer.AddRange(System.Text.Encoding.UTF8.GetBytes(requestLine));
            buffer.AddRange(body);
            return buffer.ToArray();
        }

    }
}
