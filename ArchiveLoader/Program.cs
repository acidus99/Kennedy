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
        static string ArchiveDBPath
            => ArchiveRootDir + "archive.db";

        static string PacksPath
            => ArchiveRootDir + "Packs" + Path.DirectorySeparatorChar;

        static string ArchiveRootDir = "";
        static string Operation = "";
        static string argument = "";


        static void Main(string[] args)
        {
            if(!ValidateArgs(args))
            {
                return;
            }

            switch(Operation)
            {
                case "add":
                    Console.WriteLine("Adding to archive");
                    AddCrawlToArchive(argument);
                    break;
            }
        }

        static string EnsureTrailingSlash(string path)
            => (path.EndsWith(Path.DirectorySeparatorChar)) ?
                path :
                path + Path.DirectorySeparatorChar;

        static bool ValidateArgs(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("not enough arguments");
                Console.WriteLine("Usage: [operation] [path to archive root] [[additional args]]");
                return false;
            }

            Operation = args[0].ToLower();
            ArchiveRootDir = EnsureTrailingSlash(args[1]);

            if(!File.Exists(ArchiveDBPath))
            {
                Console.WriteLine($"Could not locate archive database at '{ArchiveDBPath}'");
                return false;
            }
            if (!Directory.Exists(PacksPath))
            {
                Console.WriteLine($"Could not locate Packs directory at '{PacksPath}'");
                return false;
            }

            switch(Operation)
            {
                case "add":
                    {
                        if(args.Length != 3)
                        {
                            Console.WriteLine($"Not enough arguments for operation {Operation}");
                            Console.WriteLine($"Usage: {Operation} [path to archive root] path to crawler output to add");
                            return false;
                        }

                        argument = EnsureTrailingSlash(args[2]);
                        if (!Directory.Exists(argument))
                        {
                            Console.WriteLine($"Could need file valid crawler output at '{argument}'");
                            return false;
                        }
                        return true;
                    }

                default:
                    Console.WriteLine($"Unknown operation '{Operation}'");
                    return false;

            }
        }

        static void AddCrawlToArchive(string crawlLocation)
        {
            using (ArchiveDbContext archiveDb = new ArchiveDbContext(ArchiveDBPath))
            {
                archiveDb.Database.EnsureCreated();
            }

            SimpleDocumentIndexDbContext db = new SimpleDocumentIndexDbContext(crawlLocation);
            DocumentStore documentStore = new DocumentStore(crawlLocation + "page-store/");

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            int count = 0;
            var docs = db.Documents.Where(x => (x.Status == 20 && x.BodySaved)).ToArray();
            watch.Start();
            int added = 0;
            foreach (var doc in docs) {
                count++;
                if (count % 100 == 0)
                {
                    Console.WriteLine($"Crawl: {crawlLocation}: Processed {count} of {docs.Length}. Added to archive: {added}");
                }
                var data = documentStore.GetDocument(doc.UrlID);
                if(ArchiveEntry(doc, data))
                {
                    added++;
                }
            }
            watch.Stop();
            Console.WriteLine($"Completed processing {crawlLocation}");
            Console.WriteLine($"Total Seconds:\t{watch.Elapsed.TotalSeconds}");
            Console.WriteLine($"Snapshots Added:\t{added}");
        }

        /// <summary>
        /// returns if an entry was added into the archive or not
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        static bool ArchiveEntry(SimpleDocEntry entry, byte [] data)
        {
            ArchiveDbContext db = new ArchiveDbContext(ArchiveDBPath);
            var trueID = entry.TrueID();

            PackManager packManager = new PackManager(PacksPath);

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

            //assume that we will add this to the archive
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

            return shouldAddSnapshot;
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
