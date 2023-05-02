using System;
using System.Linq;
using EFCore.BulkExtensions;
using Gemini.Net;
using Kennedy.Data;

using Kennedy.SearchIndex.Models;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Web
{
	public class WebDatabase : IWebDatabase
	{
        

        private string StorageDirectory;


		public WebDatabase(string storageDir)
        {
            StorageDirectory = storageDir;
            //create it once it ensure we have it
            using(var context = GetContext())
            {
                context.EnsureExists();
            }
        }

        public WebDatabaseContext GetContext()
            => new WebDatabaseContext(StorageDirectory);

        public void StoreDomain(DomainInfo domainInfo)
        {
            using (var context = GetContext())
            {
                context.Domains.Add(new Domain
                {
                    DomainName = domainInfo.Domain,
                    Port = domainInfo.Port,
                    IsReachable = domainInfo.IsReachable,
                    ErrorMessage = domainInfo.ErrorMessage,
                    FaviconTxt = domainInfo.FaviconTxt,
                    RobotsUrlID = domainInfo.RobotsUrlID,
                    FaviconUrlID = domainInfo.FaviconUrlID,
                    SecurityUrlID = domainInfo.SecurityUrlID,
                });
                context.SaveChanges();
            }
        }

        public void StoreResponse(ParsedResponse parsedResponse, bool bodyWasSaved)
        {
            //store in in the doc index (inserting or updating as appropriate)
            StoreMetaData(parsedResponse, bodyWasSaved);

            //if its an image, we store extra meta data
            if (parsedResponse is ImageResponse)
            {
                StoreImageMetaData(parsedResponse as ImageResponse);
            }

            //store the links
            StoreLinks(parsedResponse);
        }

        private void StoreMetaData(ParsedResponse parsedResponse, bool bodyWasSaved)
        {

            Document entry = null;
            bool isNew = false;

            using (var context = GetContext())
            {

                entry = context.Documents
                    .Where(x => (x.UrlID == parsedResponse.RequestUrl.ID))
                    .FirstOrDefault();
                if (entry == null)
                {
                    isNew = true;
                    entry = new Document
                    {
                        UrlID = parsedResponse.RequestUrl.ID,
                        FirstSeen = System.DateTime.Now,

                        Url = parsedResponse.RequestUrl.NormalizedUrl,
                        Domain = parsedResponse.RequestUrl.Hostname,
                        Port = parsedResponse.RequestUrl.Port
                    };
                }
                entry = PopulateEntry(parsedResponse, bodyWasSaved, entry);

                if (isNew)
                {
                    context.Documents.Add(entry);
                }
                context.SaveChanges();
            }
        }

        internal void StoreImageMetaData(ImageResponse imageResponse)
        {
            using (var context = GetContext())
            {
                bool isNew = false;

                var imageEntry = context.Images
                    .Where(x => (x.UrlID == imageResponse.RequestUrl.ID))
                    .FirstOrDefault();

                if(imageEntry == null)
                {
                    isNew = true;
                    imageEntry = new Image
                    {
                        UrlID = imageResponse.RequestUrl.ID,
                    };
                }
                imageEntry.IsTransparent = imageResponse.IsTransparent;
                imageEntry.Height = imageResponse.Height;
                imageEntry.Width = imageResponse.Width;
                imageEntry.ImageType = imageResponse.ImageType;
                if (isNew)
                {
                    context.Images.Add(imageEntry);
                }
                context.SaveChanges();
            }
        }

        private void StoreLinks(ParsedResponse response)
        {
            using (var context = GetContext())
            {
                //first delete all source IDs
                context.Links.RemoveRange(context.Links
                    .Where(x => (x.SourceUrlID == response.RequestUrl.ID)));
                context.SaveChanges();

                context.Links.AddRange(response.Links.Distinct().Select(link => new DocumentLink
                {
                    SourceUrlID = response.RequestUrl.ID,
                    TargetUrlID = link.Url.ID,
                    IsExternal = link.IsExternal,
                    LinkText = link.LinkText
                }).ToList());
                context.SaveChanges();
            }
        }

        private Document PopulateEntry(ParsedResponse parsedResponse, bool bodySaved, Document entry)
        {
            entry.LastVisit = DateTime.Now;

            entry.IsAvailable = parsedResponse.IsAvailable;
            entry.Status = parsedResponse.StatusCode;
            entry.Meta = parsedResponse.Meta;

            entry.IsBodyTruncated = parsedResponse.IsBodyTruncated;
            entry.BodySize = parsedResponse.BodySize;
            entry.BodyHash = parsedResponse.BodyHash;

            entry.Charset = parsedResponse.Charset;
            entry.MimeType = parsedResponse.MimeType;
            
            entry.OutboundLinks = parsedResponse.Links.Count;

            entry.ContentType = parsedResponse.ContentType;

            if (!parsedResponse.IsFail)
            {
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
                entry.Language = null;
                entry.LineCount = 0;
                entry.Title = null;
            }

            return entry;
        }

        public bool RemoveResponse(GeminiUrl url)
        {
            bool result = false;
            using (var context = GetContext())
            {

                //first delete it from the search index database
                var document = context.Documents.Where(x => x.UrlID == url.ID).FirstOrDefault();
                if (document != null)
                {
                    context.Documents.Remove(document);
                    result = true;
                }

                //older search databases didn't have foreign key constraints, so ensure related rows are cleaned up
                var image = context.Images.Where(x => x.UrlID == url.ID).FirstOrDefault();
                if (image != null)
                {
                    context.Images.Remove(image);
                }

                var links = context.Links.Where(x => x.SourceUrlID == url.ID);
                if (links.Count() > 0)
                {
                    context.Links.RemoveRange(links);
                }
                context.SaveChanges();
            }

            return result;
        }
    }
}
