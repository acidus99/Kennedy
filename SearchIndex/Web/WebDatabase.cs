using System;
using System.Collections.Generic;
using System.Linq;
using Gemini.Net;

using Kennedy.Data;
using Kennedy.Data.RobotsTxt;

using Microsoft.Data.Sqlite;


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

        /// <summary>
        /// Stores a response in our web database. 
        /// </summary>
        /// <param name="parsedResponse"></param>
        /// <returns>whether the document is new or not. "New" documents are URLs we have neer seen, or updated content for a known URL</returns>
        public bool StoreResponse(ParsedResponse parsedResponse)
        {
            //store in in the doc index (inserting or updating as appropriate)
            bool entryUpdated = UpdateDocument(parsedResponse);

            //if the content is the same, no need to update the links since those are still accurate
            if (entryUpdated)
            {
                //store the links
                StoreLinks(parsedResponse);
            }

            return entryUpdated;
        }


        private bool UpdateDocument(ParsedResponse parsedResponse)
        {
            parsedResponse.ResponseReceived ??= DateTime.Now;
            parsedResponse.RequestSent ??= DateTime.Now;

            bool hasContentChanged = false;

            using (var db = GetContext())
            {
                Document? entry = db.Documents.Include(x => x.Image)
                    .Where(x => (x.UrlID == parsedResponse.RequestUrl.ID))
                    .FirstOrDefault();

                //do we already know about this URL?
                if (entry == null)
                {
                    hasContentChanged = true;
                    entry = new Document(parsedResponse.RequestUrl)
                    {
                        FirstSeen = parsedResponse.ResponseReceived.Value
                    };
                    db.Documents.Add(entry);
                }
                else
                {
                    //is this a re-import of the same content? if so the timestamps will match
                    if (entry.LastVisit == parsedResponse.ResponseReceived)
                    {
                        //nothing to do, and nothing has changed
                        return false;
                    }
                    else if (parsedResponse.ResponseReceived < entry.LastVisit)
                    {
                        //updating with an older response,
                        //so just update the historical discovery times. No content has changed

                        bool updatedTimes = false;

                        if (entry.FirstSeen > parsedResponse.ResponseReceived)
                        {
                            entry.FirstSeen = parsedResponse.ResponseReceived.Value;
                            updatedTimes = true;
                        }

                        if (parsedResponse.IsFail &&
                            entry.LastSuccessfulVisit < parsedResponse.ResponseReceived)
                        {
                            entry.LastSuccessfulVisit = parsedResponse.ResponseReceived;
                            updatedTimes = true;
                        }

                        if(updatedTimes)
                        {
                            db.SaveChanges();
                        }
                        return false;
                    }
                    else
                    {
                        //newer content so we need to update the document's entry
                        //how much needs to be updated depends on if the response is different
                        hasContentChanged = (parsedResponse.Hash != entry.ResponseHash);
                    }
                }

                //ready to prepare oure DTOs

                //Always update the most recent visit time
                entry.LastVisit = parsedResponse.ResponseReceived.Value;

                if (!parsedResponse.IsFail)
                {
                    entry.LastSuccessfulVisit = parsedResponse.ResponseReceived;
                }

                //everything else is only updated if the content has changed
                if (hasContentChanged)
                {
                    entry.IsAvailable = parsedResponse.IsAvailable;
                    entry.StatusCode = parsedResponse.StatusCode;
                    entry.Meta = parsedResponse.Meta;

                    entry.IsBodyIndexed = (parsedResponse is ITextResponse && ((ITextResponse)parsedResponse).HasIndexableText);

                    entry.IsBodyTruncated = parsedResponse.IsBodyTruncated;
                    entry.BodySize = parsedResponse.BodySize;
                    entry.BodyHash = parsedResponse.BodyHash;
                    entry.ResponseHash = parsedResponse.Hash;

                    entry.Charset = parsedResponse.Charset;
                    entry.MimeType = parsedResponse.MimeType;

                    entry.OutboundLinks = parsedResponse.Links.Count;

                    entry.ContentType = parsedResponse.ContentType;

                    entry.Language = parsedResponse.Language;
                    entry.DetectedLanguage = null;
                    entry.LineCount = 0;
                    entry.Title = null;


                    //extra meta data
                    if (parsedResponse is GemTextResponse)
                    {
                        var gemtext = (GemTextResponse)parsedResponse;

                        entry.DetectedLanguage = gemtext.DetectedLanguage;
                        entry.LineCount = gemtext.LineCount;
                        entry.Title = gemtext.Title;
                    }
                    else if (parsedResponse is PlainTextResponse)
                    {
                        var txtDoc = (PlainTextResponse)parsedResponse;

                        entry.DetectedLanguage = txtDoc.DetectedLanguage;
                        entry.LineCount = txtDoc.LineCount;
                    }

                    if (parsedResponse is ImageResponse)
                    {
                        var image = (ImageResponse)parsedResponse;

                        if (entry.Image == null)
                        {
                            entry.Image = new Image()
                            {
                                UrlID = parsedResponse.RequestUrl.ID,
                                IsTransparent = image.IsTransparent,
                                Height = image.Height,
                                Width = image.Width,
                                ImageType = image.ImageType
                            };
                        }
                        else
                        {
                            entry.Image.IsTransparent = image.IsTransparent;
                            entry.Image.Height = image.Height;
                            entry.Image.Width = image.Width;
                            entry.Image.ImageType = image.ImageType;
                        }
                    }
                    //not an image, but existing entry had image meta data, so explicitly remove it
                    else if (entry.Image != null)
                    {
                        db.Images.Remove(entry.Image);
                        entry.Image = null;
                    }
                }
                db.SaveChanges();
            }

            //only need to update special files if the content has changed
            if (hasContentChanged)
            {
                UpdateSpecialFiles(parsedResponse);
            }

            //propogate our if the content has changed to control re-indexing FTS and other operations
            return hasContentChanged;
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
            bool isRemove = !(response.IsSuccess && IsValidRobotsTxtFile(response?.BodyText));

            using (var context = GetContext())
            {
                //see if it already exists
                var entry = context.RobotsTxts
                    .Where(x => (x.Protocol == response!.RequestUrl.Protocol &&
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
                        entry = new RobotsTxt(response!.RequestUrl)
                        {
                            Content = response.BodyText
                        };
                        context.RobotsTxts.Add(entry);
                    }
                    else
                    {
                        entry.Content = response!.BodyText;
                    }
                }
                context.SaveChanges();
            }
        }

        private void UpdateFavicon(ParsedResponse response)
        {

            //bool isRemove = !(response.IsSuccess && IsValidFavicon(response.BodyText.Trim()));

            //using (var context = GetContext())
            //{
            //    //see if it already exists
            //    var entry = context.Favicons
            //        .Where(x => (x.Protocol == response.RequestUrl.Protocol &&
            //                    x.Domain == response.RequestUrl.Hostname &&
            //                    x.Port == response.RequestUrl.Port))
            //        .FirstOrDefault();

            //    if (isRemove)
            //    {
            //        if(entry != null)
            //        {
            //            context.Favicons.Remove(entry);
            //        }
            //    }
            //    else
            //    {

            //        //if not, create a stub
            //        if (entry == null)
            //        {
            //            entry = new Favicon(response.RequestUrl);
            //            context.Favicons.Add(entry);
            //        }

            //        entry.Emoji = response.BodyText.Trim();
            //    }
            //    context.SaveChanges();
            //}
        }

        private void UpdateSecurity(ParsedResponse response)
        {
            bool isRemove = !(response.IsSuccess && IsValidSecurity(response.BodyText));

            using (var context = GetContext())
            {
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
                        entry = new SecurityTxt(response.RequestUrl)
                        {
                            Content = response.BodyText
                        };
                        context.SecurityTxts.Add(entry);
                    }
                    else
                    {
                        entry.Content = response.BodyText;
                    }
                }
                context.SaveChanges();
            }
        }

        private bool IsValidFavicon(string contents)
            => (contents != null && !contents.Contains(" ") && !contents.Contains("\n") && contents.Length < 20);

        private bool IsValidSecurity(string contents)
            => (contents != null && contents.ToLower().Contains("contact:"));


        private bool IsValidRobotsTxtFile(string? contents)
        {
            if (contents != null)
            {
                RobotsTxtFile robotsTxt = new RobotsTxtFile(contents);
                return !robotsTxt.IsMalformed;
            }
            return false;
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
    }
}
