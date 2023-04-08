using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Models;
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
            var db = new SearchIndexContext(storagePath);
        }

        public string GetImageIndexText(long dbDocId)
        {
            using (var db = GetContext())
            {
                using (var connection = (db.Database.GetDbConnection()))
                {
                    connection.Open();
                    var cmd = db.Database.GetDbConnection().CreateCommand();
                    cmd.CommandText = "SELECT Terms FROM ImageSearch WHERE ROWID = " + dbDocId;
                    return (string)cmd.ExecuteScalar();
                }
            }
        }

        public SearchIndexContext GetContext()
            => new SearchIndexContext(StoragePath);

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
            using (var db = GetContext())
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

        internal void DeleteDocument(GeminiUrl url)
        {
            //first delete it from the search index database
            using (var db = GetContext())
            {
                var document = db.Documents.Where(x => x.UrlID == url.ID).FirstOrDefault();
                if (document != null)
                {
                    db.Documents.Remove(document);
                }

                //older search databases didn't have foreign key constraints, so ensure related rows are cleaned up

                var image = db.Images.Where(x => x.UrlID == url.ID).FirstOrDefault();
                if(image != null)
                {
                    db.Images.Remove(image);
                }

                var links = db.Links.Where(x => x.SourceUrlID == url.ID);
                if(links.Count() > 0)
                {
                    db.Links.RemoveRange(links);
                }
                //need to mak

            }
        }

        internal void StoreImageMetaData(ImageResponse imageResponse)
        {
            using (var db = GetContext())
            {
                Image imageEntry = new Image
                {
                    UrlID = imageResponse.RequestUrl.ID,
                    IsTransparent = imageResponse.IsTransparent,
                    Height = imageResponse.Height,
                    Width = imageResponse.Width,
                    ImageType = imageResponse.ImageType
                };
                db.Images.Add(imageEntry);
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
            using (var db = GetContext())
            {
                //first delete all source IDs
                db.Links.RemoveRange(db.Links
                    .Where(x => (x.SourceUrlID == response.RequestUrl.ID)));
                db.SaveChanges();
                db.BulkInsert(response.Links.Distinct().Select(link => new DocumentLink
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
