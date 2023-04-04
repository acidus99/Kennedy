using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Db;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Kennedy.SearchIndex
{ 
    public class DocumentIndex
    {
        string StoragePath;

        public DocumentIndex(string storagePath)
        {
            StoragePath = storagePath;
            //create and destory a DBContext to force the DB to be there
            var db = new SearchIndexDbContext(storagePath);
            db.Database.EnsureCreated();
            EnsureFullTextSearch(db);
        }

        private void EnsureFullTextSearch(SearchIndexDbContext db)
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

                cmd.CommandText = "SELECT Count(*) FROM sqlite_master WHERE type='table' AND name='ImageSearch';";
                count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count == 0)
                {
                    cmd.CommandText = "CREATE VIRTUAL TABLE ImageSearch using fts5(Terms, tokenize = 'porter');";
                    cmd.ExecuteNonQuery();
                }

            }
        }

        public string GetImageIndexText(long dbDocId)
        {
            var db = new SearchIndexDbContext(StoragePath);

            using (var connection = (db.Database.GetDbConnection()))
            {
                connection.Open();
                var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT Terms FROM ImageSearch WHERE ROWID = " + dbDocId;
                return (string)cmd.ExecuteScalar();
            }
        }

        public SearchIndexDbContext GetContext()
            => new SearchIndexDbContext(StoragePath);

        public void Close()
        {
            ///nop since we are not caching any writes
        }

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
        internal void StoreMetaData(ParsedResponse parsedResponse, bool bodySaved)
        {
            Document entry = null;
            using (var db = new SearchIndexDbContext(StoragePath))
            {
                bool isNew = false;

                entry = db.Documents
                    .Where(x => (x.UrlID == parsedResponse.RequestUrl.ID))
                    .FirstOrDefault();
                if (entry == null)
                {
                    isNew = true;
                    entry = new Document
                    {
                        UrlID = parsedResponse.RequestUrl.ID,
                        FirstSeen = System.DateTime.Now,

                        ErrorCount = 0,

                        Url = parsedResponse.RequestUrl.NormalizedUrl,
                        Domain = parsedResponse.RequestUrl.Hostname,
                        Port = parsedResponse.RequestUrl.Port
                    };
                }
                entry = PopulateEntry(parsedResponse, bodySaved, entry);

                if(isNew)
                {
                    db.Documents.Add(entry);
                }
                db.SaveChanges();
            }
        }

        internal void StoreImageMetaData(ImageResponse imageResponse)
        {
            using (var db = new SearchIndexDbContext(StoragePath))
            {
                StoredImageEntry imageEntry = new StoredImageEntry
                {
                    UrlID = imageResponse.RequestUrl.ID,
                    IsTransparent = imageResponse.IsTransparent,
                    Height = imageResponse.Height,
                    Width = imageResponse.Width,
                    ImageType = imageResponse.ImageType
                };
                db.ImageEntries.Add(imageEntry);
                db.SaveChanges();
            }
        }

        private Document PopulateEntry(ParsedResponse parsedResponse, bool bodySaved, Document entry)
        {
            entry.LastVisit = DateTime.Now;

            entry.ConnectStatus = parsedResponse.ConnectStatus;
            entry.Status = parsedResponse.StatusCode;
            entry.Meta = parsedResponse.Meta;

            entry.MimeType = parsedResponse.MimeType;
            entry.BodySkipped = parsedResponse.BodySkipped;
            entry.BodySaved = bodySaved;
            entry.BodySize = parsedResponse.BodySize;
            entry.BodyHash = parsedResponse.BodyHash;
            entry.OutboundLinks = parsedResponse.Links.Count;

            entry.ConnectTime = parsedResponse.ConnectTime;
            entry.DownloadTime = parsedResponse.DownloadTime;
            entry.ContentType = parsedResponse.ContentType;

            if (IsError(parsedResponse))
            {
                entry.ErrorCount++;
            }
            else
            {
                entry.ErrorCount = 0;
                entry.LastSuccessfulVisit = entry.LastVisit;
            }

            //extra meta data
            if (parsedResponse is GemTextResponse)
            {
                var gemtext = (GemTextResponse)parsedResponse;

                entry.Language = gemtext.Language;
                entry.LineCount = gemtext.LineCount;
                entry.Title = gemtext.Title;
            }
            else
            {
                entry.Language = "";
                entry.LineCount = 0;
                entry.Title = "";
            }

            return entry;
        }

        /// <summary>
        /// does this response represent an error?
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        private bool IsError(ParsedResponse resp)
            => (resp.ConnectStatus != ConnectStatus.Success) ||
                resp.IsTempFail || resp.IsPermFail;

        internal void StoreLinks(ParsedResponse response)
        {
            using (var db = new SearchIndexDbContext(StoragePath))
            {
                //first delete all source IDs
                db.LinkEntries.RemoveRange(db.LinkEntries
                    .Where(x => (x.SourceUrlID == response.RequestUrl.ID)));
                db.SaveChanges();
                db.BulkInsert(response.Links.Distinct().Select(link => new StoredLinkEntry
                {
                    SourceUrlID = response.RequestUrl.ID,
                    TargetUrlID = link.Url.ID,
                    IsExternal = link.IsExternal,
                    LinkText = link.LinkText
                }).ToList());
                db.SaveChanges();
            }
        }
    }
}
