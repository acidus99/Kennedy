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

        public bool StoreResponse(ParsedResponse parsedResponse)
        {
            //store in in the doc index (inserting or updating as appropriate)
            bool entryUpdated = UpdateDocuments(parsedResponse);

            //if its an image, we store extra meta data
            if (entryUpdated && parsedResponse is ImageResponse)
            {
                UpdateImageMetadata(parsedResponse as ImageResponse);
            }

            //if the content is the same, no need to update the links since those are still accurate
            if (entryUpdated)
            {
                //store the links
                StoreLinks(parsedResponse);
            }

            return entryUpdated;
        }

        private bool UpdateDocuments(ParsedResponse parsedResponse)
        {
            bool isDiffResponse = false;
            Document entry = null;
            bool isNew = false;

            using (var context = GetContext())
            {
                //see if it already exists
                entry = context.Documents
                    .Where(x => (x.UrlID == parsedResponse.RequestUrl.ID))
                    .FirstOrDefault();

                //is this a dupe of the existing data or is it older?
                if (entry?.LastVisit >= parsedResponse.ResponseReceived)
                {
                    return false;
                }

                //if not, create a stub
                if (entry == null)
                {
                    isNew = true;
                    entry = new Document(parsedResponse.RequestUrl)
                    {
                        FirstSeen = parsedResponse.ResponseReceived
                    };
                }

                isDiffResponse = IsResponseDifferent(parsedResponse, entry);

                entry = PopulateEntry(parsedResponse, entry, isDiffResponse);

                if (isNew)
                {
                    context.Documents.Add(entry);
                }
                context.SaveChanges();
            }

            //update the robots/security/favicon
            UpdateSpecialFiles(parsedResponse);

            return isDiffResponse;
        }

        private bool IsResponseDifferent(ParsedResponse response, Document existingEntry)
        {
            if(response.StatusCode != existingEntry.StatusCode)
            {
                return true;
            }

            if (response.Meta != existingEntry.Meta)
            {
                return true;
            }

            //new has body, old doesn't
            if(response.BodyHash.HasValue && !existingEntry.BodyHash.HasValue)
            {
                return true;
            }
            //old has body, new doesn't
            if(!response.BodyHash.HasValue && existingEntry.BodyHash.HasValue)
            {
                return true;
            }
            return (response.BodyHash == existingEntry.BodyHash);
        }

        /// <summary>
        /// Handles updating the Favicon, Security, or Robots tables
        /// </summary>
        /// <param name="response"></param>
        private void UpdateSpecialFiles(ParsedResponse response)
        {
            if (response.RequestUrl.Path == "/robots.txt")
            {
                UpdateRobots(response);
            }
            else if (response.RequestUrl.Path == "/favicon.txt")
            {
                UpdateFavicon(response);
            }
            else if (response.RequestUrl.Path == "/.well-known/security.txt")
            {
                UpdateSecurity(response);
            }
        }

        private void UpdateRobots(ParsedResponse response)
        {
            bool isRemove = !(response.IsSuccess && response.HasBody);

            using (var context = GetContext())
            {
                bool isNew = false;
                //see if it already exists
                var entry = context.RobotsTxts
                    .Where(x => (x.Protocol == response.RequestUrl.Protocol &&
                                x.Domain == response.RequestUrl.Hostname &&
                                x.Port == response.RequestUrl.Port))
                    .FirstOrDefault();

                if (isRemove)
                {
                    if (entry != null)
                    {
                        //remove it
                        context.RobotsTxts.Remove(entry);
                    }
                }
                else
                {
                    //if not, create a stub
                    if (entry == null)
                    {
                        isNew = true;
                        entry = new RobotsTxt(response.RequestUrl);
                    }

                    entry.Content = response.BodyText;

                    if (isNew)
                    {
                        context.RobotsTxts.Add(entry);
                    }
                }
                context.SaveChanges();
            }
        }

        private void UpdateFavicon(ParsedResponse response)
        {

            bool isRemove = !(response.IsSuccess && IsValidFavicon(response.BodyText));

            using (var context = GetContext())
            {
                bool isNew = false;
                //see if it already exists
                var entry = context.Favicons
                    .Where(x => (x.Protocol == response.RequestUrl.Protocol &&
                                x.Domain == response.RequestUrl.Hostname &&
                                x.Port == response.RequestUrl.Port))
                    .FirstOrDefault();

                if (isRemove)
                {
                    if(entry != null)
                    {
                        context.Favicons.Remove(entry);
                    }
                }
                else
                {

                    //if not, create a stub
                    if (entry == null)
                    {
                        isNew = true;
                        entry = new Favicon(response.RequestUrl);
                    }

                    entry.Emoji = response.BodyText;

                    if (isNew)
                    {
                        context.Favicons.Add(entry);
                    }
                }
                context.SaveChanges();
            }
        }

        private void UpdateSecurity(ParsedResponse response)
        {
            bool isRemove = !(response.IsSuccess && IsValidSecurity(response.BodyText));

            using (var context = GetContext())
            {
                bool isNew = false;
                //see if it already exists
                var entry = context.SecurityTxts
                    .Where(x => (x.Protocol == response.RequestUrl.Protocol &&
                                x.Domain == response.RequestUrl.Hostname &&
                                x.Port == response.RequestUrl.Port))
                    .FirstOrDefault();

                if (isRemove)
                {
                    if(entry != null)
                    {
                        context.SecurityTxts.Remove(entry);
                    }
                }
                else
                {
                    //if not, create a stub
                    if (entry == null)
                    {
                        isNew = true;
                        entry = new SecurityTxt(response.RequestUrl);
                    }

                    entry.Content = response.BodyText;

                    if (isNew)
                    {
                        context.SecurityTxts.Add(entry);
                    }
                    context.SaveChanges();
                }
            }
        }

        private bool IsValidFavicon(string contents)
            => (contents != null && !contents.Contains(" ") && !contents.Contains("\n") && contents.Length < 20);

        private bool IsValidSecurity(string contents)
            => (contents != null && contents.ToLower().Contains("contact:"));

        private void UpdateImageMetadata(ImageResponse imageResponse)
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

        private Document PopulateEntry(ParsedResponse parsedResponse, Document entry, bool isDiffResponse)
        {
            entry.LastVisit = parsedResponse.ResponseReceived;

            if (isDiffResponse)
            {
                entry.IsAvailable = parsedResponse.IsAvailable;
                entry.StatusCode = parsedResponse.StatusCode;
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

                    entry.DetectedLanguage = gemtext.DetectedLanguage;
                    entry.Language = gemtext.Language;
                    entry.LineCount = gemtext.LineCount;
                    entry.Title = gemtext.Title;
                }
                else
                {
                    entry.Language = parsedResponse.Language;
                    entry.DetectedLanguage = null;
                    entry.LineCount = 0;
                    entry.Title = null;
                }
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
