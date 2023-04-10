using System;

using HashDepot;

using Gemini.Net;
using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.Data;


namespace Kennedy.Archive
{
	public class Archiver
	{
        public ArchiveDbContext Context { get; private set; }

        PackManager packManager;

        string ArchiveDBPath;

		string PacksPath;

        public Archiver(string archiveDB, string packsPath)
		{
			Context = new ArchiveDbContext(archiveDB);
            Context.Database.EnsureCreated();
            packManager = new PackManager(packsPath);
        }

		public bool ArchiveContent(DateTime captured, GeminiUrl url, int statusCode, string meta, byte[] contentData, bool isPublic = true)
		{
            var urlEntry = Context.Urls.Where(x => x.Id == url.ID).FirstOrDefault();
            bool newUrl = false;
            if (urlEntry == null)
            {
                urlEntry = new Url(url)
                {
                    IsPublic = isPublic
                };
                Context.Urls.Add(urlEntry);
                newUrl = true;
                //need to save for foreign key constraint
                Context.SaveChanges();
            }

            var packFile = packManager.GetPack(urlEntry.PackName);

            if (newUrl)
            {
                packFile.Append(PackRecordFactory.MakeInfoRecord(urlEntry.FullUrl));
            }

            //OK, create a new snapshot
            var dataHash = GetDataHash(contentData);

            var previousSnapshot = Context.Snapshots
                .Where(x => x.UrlId == urlEntry.Id &&
                        x.DataHash == dataHash).FirstOrDefault();

            //assume that we will add this to the archive
            bool shouldAddSnapshot = true;

            var snapshot = new Snapshot
            {
                Captured = captured,
                StatusCode = statusCode,
                Size = contentData.LongLength,
                ContentType = GetContentType(statusCode, meta),
                Meta = meta,
                DataHash = dataHash,
                Url = urlEntry,
                UrlId = urlEntry.Id
            };

            if (previousSnapshot == null)
            {
                //write it into the Pack
                snapshot.Offset = packFile.Append(PackRecordFactory.MakeOptimalRecord(contentData));
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
                urlEntry.Snapshots.Add(snapshot);
                Context.Urls.Update(urlEntry);
                Context.Snapshots.Add(snapshot);
                Context.SaveChanges();
            }

            return shouldAddSnapshot;
        }

        private long GetDataHash(byte[] body)
        {
            //want signed long, so use 32 bit hash.
            return Convert.ToInt64(XXHash.Hash32(body));
        }

        static ContentType GetContentType(int status, string mimeType)
        {
            if (status == 20)
            {
                if (mimeType.StartsWith("text/"))
                {
                    return ContentType.Text;
                }
                else if (mimeType.StartsWith("image/"))
                {
                    return ContentType.Image;
                }
                return ContentType.Binary;
            }
            return ContentType.Unknown;
        }

        public bool RemoveContent(GeminiUrl gurl)
        {
            var url = Context.Urls.Where(x => x.Id == gurl.ID).FirstOrDefault();
            if (url != null)
            {
                Context.Urls.Remove(url);
                Context.SaveChanges();
                packManager.DeletePack(url.PackName);
                return true;
            }
            return false;
        }
    }
}

