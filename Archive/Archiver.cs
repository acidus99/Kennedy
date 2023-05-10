using System;

using HashDepot;

using Gemini.Net;
using Kennedy.Archive.Db;
using Kennedy.Archive.Pack;
using Kennedy.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Archive
{
	public class Archiver
	{
        SnapshotReader snapshotReader;
        PackManager packManager;
        string ArchiveDBPath;

        public Archiver(string archiveDB, string packsPath)
		{
            ArchiveDBPath = archiveDB;
            using (var db = GetContext())
            {
                db.Database.EnsureCreated();
            }
            packManager = new PackManager(packsPath);
            snapshotReader = new SnapshotReader(packManager);
        }

        public ArchiveDbContext GetContext()
            => new ArchiveDbContext(ArchiveDBPath);

        private bool AlreadyInArchive(GeminiResponse response)
        {
            using (var db = GetContext())
            {

                //are the capture times the same? If so, don't save it, because we are adding something
                //that has already been added.
                if (db.Snapshots
                    .Where(x => x.UrlId == response.RequestUrl.ID && x.Captured == response.ResponseReceived)
                    .Any())
                {
                    return true;
                }
                return false;
            }
        }

        private bool CanBeArchived(GeminiResponse response)
        {
            if (response.IsConnectionError)
            {
                return false;
            }
            else if (response.IsSuccess && response.HasBody)
            {
                return true;
            }
            else if (response.IsInput ||
                response.IsRedirect ||
                response.IsAuth)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Archives a response without a body
        /// </summary>
        /// <param name="captured"></param>
        /// <param name="url"></param>
        /// <param name="statusCode"></param>
        /// <param name="meta"></param>
        /// <param name="isPublic"></param>
        /// <returns></returns>
        public bool ArchiveResponse(GeminiResponse response, bool isPublic = true)
        {
            if (!CanBeArchived(response))
            {
                return false;
            }
            if (AlreadyInArchive(response))
            {
                return false;
            }

            using (var db = GetContext())
            {
                var urlEntry = db.Urls.Where(x => x.Id == response.RequestUrl.ID).FirstOrDefault();
                bool newUrl = false;
                if (urlEntry == null)
                {
                    urlEntry = new Url(response.RequestUrl)
                    {
                        IsPublic = isPublic
                    };
                    db.Urls.Add(urlEntry);
                    newUrl = true;
                    //need to save for foreign key constraint
                    db.SaveChanges();
                }

                var packFile = packManager.GetPack(urlEntry.PackName);

                if (newUrl)
                {
                    packFile.Append(PackRecordFactory.MakeInfoRecord(urlEntry.FullUrl));
                }

                var respBytes = GeminiParser.CreateResponseBytes(response);

                var dataHash = GetDataHash(respBytes);

                var previousSnapshot = db.Snapshots
                    .Where(x => x.UrlId == urlEntry.Id &&
                            x.DataHash == dataHash).FirstOrDefault();

                //OK, create a new snapshot
                var snapshot = new Snapshot
                {
                    Captured = response.ResponseReceived,
                    IsDuplicate = false,
                    HasBodyContent = response.HasBody,
                    StatusCode = response.StatusCode,
                    Size = respBytes.LongLength,
                    Mimetype = response.MimeType,
                    DataHash = dataHash,
                    Url = urlEntry,
                    UrlId = urlEntry.Id,
                    IsBodyTruncated = response.IsBodyTruncated
                };

                if (previousSnapshot == null)
                {
                    //this snapshot is unique, so record it
                    snapshot.Offset = packFile.Append(PackRecordFactory.MakeOptimalRecord(respBytes));
                }
                else
                {
                    //is same as existing
                    snapshot.Offset = previousSnapshot.Offset;
                    snapshot.IsDuplicate = true;
                }
                db.Snapshots.Add(snapshot);
                db.SaveChanges();
                return true;
            }
        }

        public ArchiveStats GetArchiveStats()
        {
            var ret = new ArchiveStats();

            using (var db = GetContext())
            {

                ret.Domains = db.Urls
                    .Select(x => new { Domain = x.Domain, Port = x.Port })
                    .Distinct()
                    .LongCount();

                ret.UrlsPublic = db.Urls
                    .Where(x => x.IsPublic)
                    .LongCount();

                ret.UrlsExcluded = db.Urls
                    .Where(x => !x.IsPublic)
                    .LongCount();

                ret.Captures = db.Snapshots.LongCount();

                ret.CapturesUnique = db.Snapshots
                    .Where(x => !x.IsDuplicate)
                    .LongCount();

                ret.Size = db.Snapshots
                    .Where(x => !x.IsDuplicate)
                    .Sum(x => x.Size);

                ret.SizeWithoutDeDuplication = db.Snapshots
                    .Sum(x => x.Size);

                var captures = db.Snapshots.Select(x => x.Captured);

                ret.OldestSnapshot = captures.Min();
                ret.NewestSnapshot = captures.Max();
            }

            return ret;
        }

        public GeminiResponse? GetLatestResponse(long  urlID)
        {
            Snapshot? snapshot = null;

            using (var db = GetContext())
            {
                snapshot = db.Snapshots
                    .Where(x => x.UrlId == urlID)
                    .OrderByDescending(x => x.Captured)
                    .Include(x=>x.Url)
                    .FirstOrDefault();
            }

            if(snapshot == null)
            {
                return null;
            }
            return snapshotReader.ReadResponse(snapshot);
        }


        private long GetDataHash(byte[] bytes)
        {
            //want signed long
            return unchecked((long)XXHash.Hash64(bytes));
        }
    }
}

