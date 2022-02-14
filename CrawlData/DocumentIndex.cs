
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.CrawlData.Db;

namespace Kennedy.CrawlData
{ 
    public class DocumentIndex
    {
        string StoragePath;

        public DocumentIndex(string storagePath)
        {
            StoragePath = storagePath;
            //create and destory a DBContext to force the DB to be there
            var db = new DocIndexDbContext(storagePath);
            db.Database.EnsureCreated();
            EnsureFullTextSearch(db);
        }

        public void EnsureFullTextSearch(DocIndexDbContext db)
        {

            using (var connection = db.Database.GetDbConnection())
            {
                connection.Open();
                var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT Count(*) FROM sqlite_master WHERE type='table' AND name='FTS';";
                var count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count == 0)
                {
                    cmd.CommandText = "CREATE VIRTUAL TABLE FTS using fts5(Title, Body, tokenize = 'porter');";
                    cmd.ExecuteNonQuery();
                }
            }
        }


        public DocIndexDbContext GetContext()
            => new DocIndexDbContext(StoragePath);

        public void Close()
        {
            ///nop since we are not caching any writes
        }

        public static long toLong(ulong ulongValue)
            => unchecked((long)ulongValue);

        public static ulong toULong(long longValue)
            => unchecked((ulong)longValue);

        /// <summary>
        /// Returns the DBDocID just for fun
        /// </summary>
        /// <param name="url"></param>
        /// <param name="resp"></param>
        /// <param name="docTitle"></param>
        /// <param name="outboundLinkCount"></param>
        /// <param name="bodySaved"></param>
        /// <param name="lines"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public long StoreMetaData(GeminiResponse resp, string docTitle, int outboundLinkCount, bool bodySaved, string language, int lineCount)
        {
            var dbDocID = toLong(resp.RequestUrl.DocID);

            using (var db = new DocIndexDbContext(StoragePath))
            {
                bool isNew = false;

                var entry = db.DocEntries.Where(x => (x.DBDocID == dbDocID)).FirstOrDefault();
                if (entry == null)
                {
                    isNew = true;
                    entry = new StoredDocEntry
                    {
                        DBDocID = dbDocID,
                        FirstSeen = System.DateTime.Now,

                        ErrorCount = 0,

                        Url = resp.RequestUrl.NormalizedUrl,
                        Domain = resp.RequestUrl.Hostname,
                        Port = resp.RequestUrl.Port
                    };
                }
                entry.LastVisit = System.DateTime.Now;

                entry.ConnectStatus = resp.ConnectStatus;
                entry.Status = resp.StatusCode;
                entry.Meta = resp.Meta;

                entry.MimeType = resp.MimeType;
                entry.BodySkipped = resp.BodySkipped;
                entry.BodySaved = bodySaved;
                entry.BodySize = resp.BodySize;
                entry.BodyHash = resp.BodyHash;
                entry.Title = docTitle;

                entry.LineCount = lineCount;
                entry.Language = language;

                entry.ConnectTime = resp.ConnectTime;
                entry.DownloadTime = resp.DownloadTime;
                entry.OutboundLinks = outboundLinkCount;

                if (IsError(resp))
                {
                    entry.ErrorCount++;
                }
                else
                {
                    entry.ErrorCount = 0;
                    entry.LastSuccessfulVisit = entry.LastVisit;
                }

                if(isNew)
                {
                    db.DocEntries.Add(entry);
                }
                db.SaveChanges();
                return dbDocID;
            }
        }

        /// <summary>
        /// does this response represent an error?
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        private bool IsError(GeminiResponse resp)
            => (resp.ConnectStatus != ConnectStatus.Success) ||
                resp.IsTempFail || resp.IsPermFail;

        public void StoreLinks(GeminiUrl sourcePage, List<FoundLink> links)
        {
            using (var db = new DocIndexDbContext(StoragePath))
            {
                var dbDocID = toLong(sourcePage.DocID);
                //first delete all source IDs
                db.LinkEntries.RemoveRange(db.LinkEntries.Where(x => (x.DBSourceDocID == dbDocID)));
                db.SaveChanges();
                db.BulkInsert(links.Distinct().Select(link => new StoredLinkEntry
                {
                    DBSourceDocID = toLong(sourcePage.DocID),
                    DBTargetDocID = toLong(link.Url.DocID),
                    IsExternal = link.IsExternal,
                    LinkText = link.LinkText
                }).ToList());
                db.SaveChanges();
            }
        }

        public List<uint> GetBodyHashes()
        {
            using (var db = new DocIndexDbContext(StoragePath))
            {
                return db.DocEntries
                        .Select(x => x.BodyHash)
                        .Where(x => (x != null))
                        .Select(x => x.Value).ToList();
            }
        }

        /// <summary>
        /// Gets URLs we have stored in the db
        /// </summary>
        /// <returns></returns>
        public List<GeminiUrl> GetUrls()
        {
            using (var db = new DocIndexDbContext(StoragePath))
            {
                return db.DocEntries
                        .Select(x => x.Url)
                        .Select(x => new GeminiUrl(x)).ToList();
            }
        }

    }
}
