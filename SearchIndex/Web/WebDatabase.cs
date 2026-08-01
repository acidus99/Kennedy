using System;
using System.Collections.Generic;
using System.Linq;
using Gemini.Net;
using Kennedy.Data;
using Kennedy.Data.RobotsTxt;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Utils;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Web;

public class WebDatabase : IWebDatabase
{
    private string StorageDirectory;

    int contextOperations = 0;
    const int BatchSize = 3000;
    Dictionary<long, bool> seenUrls;
    WebDatabaseContext bulkContext;

    public WebDatabase(string storageDir)
    {
        StorageDirectory = storageDir;
        bulkContext = GetContext();
        bulkContext.EnsureExists();
        seenUrls = new Dictionary<long, bool>();
    }

    public WebDatabaseContext GetContext()
        => new WebDatabaseContext(StorageDirectory);

    public void FinalizeStores()
    {
        FlushContext();
    }

    private void FlushContext()
    {
        bulkContext.SaveChanges();
        contextOperations = 0;
        bulkContext = GetContext();
        seenUrls.Clear();
    }

    /// <summary>
    /// Stores a response in our web database.
    /// </summary>
    /// <param name="parsedResponse"></param>
    /// <returns>whether the document is new or not. "New" documents are URLs we have neer seen, or updated content for a known URL</returns>
    public FtsIndexAction StoreResponse(ParsedResponse parsedResponse)
    {
        contextOperations++;

        //store in in the doc index (inserting or updating as appropriate)
        (FtsIndexAction action, bool isContentNewOrChanged) = UpdateDocument(parsedResponse);

        //if the content changed, we need to update the links table for this response
        if (isContentNewOrChanged)
        {
            StoreLinks(parsedResponse);
        }

        if (contextOperations > BatchSize)
        {
            FlushContext();
        }

        return action;
    }

    private (FtsIndexAction action, bool isContentNewOrChanged) UpdateDocument(ParsedResponse parsedResponse)
    {
        //assume no FTS updates, and that the content has not changed
        //most Gemini content does not change
        FtsIndexAction action = FtsIndexAction.Nothing;
        bool isContentNewOrChanged = false;

        parsedResponse.ResponseReceived ??= DateTime.Now;
        parsedResponse.RequestSent ??= DateTime.Now;

        //This is rare. Means we have seen the same URL, during the current bulk operation.
        //flush and keep going
        if (seenUrls.ContainsKey(parsedResponse.RequestUrl.ID))
        {
            FlushContext();
        }
        seenUrls[parsedResponse.RequestUrl.ID] = true;

        Document? entry = bulkContext.Documents.Include(x => x.Image)
            .Where(x => (x.UrlID == parsedResponse.RequestUrl.ID))
            .FirstOrDefault();
        bool isNewDocument = entry == null;
        bool wasAvailable = entry?.IsAvailable ?? false;

        //do we already know about this URL?
        if (entry == null)
        {
            //brand new
            isContentNewOrChanged = true;
            action = FtsIndexAction.AddCurrent;
            entry = new Document(parsedResponse.RequestUrl)
            {
                FirstSeen = parsedResponse.ResponseReceived.Value,
                LastTimeUpdated = parsedResponse.ResponseReceived.Value,
            };
            bulkContext.Documents.Add(entry);
        }
        else
        {
            //is this a re-import of the same content? if so the timestamps will match
            if (entry.LastVisit == parsedResponse.ResponseReceived)
            {
                //nothing has changed, so nothing to index, and no links to add
                return (FtsIndexAction.Nothing, false);
            }
            else if (parsedResponse.ResponseReceived < entry.LastVisit)
            {
                //updating with an older response,
                //so just update the historical discovery times. No content has changed
                if (entry.FirstSeen > parsedResponse.ResponseReceived)
                {
                    entry.FirstSeen = parsedResponse.ResponseReceived.Value;
                }
                //if responses are the same, and this is an early visit, push back the last time updated
                if (entry.ResponseHash == parsedResponse.Hash)
                {
                    entry.LastTimeUpdated = parsedResponse.ResponseReceived.Value;
                }

                return (FtsIndexAction.Nothing, false);
            }

            if (parsedResponse.Hash != entry.ResponseHash)
            {
                isContentNewOrChanged = true;
                entry.LastTimeUpdated = parsedResponse.ResponseReceived.Value;

                //what FTS action happens depends on what's changed from the previous
                if (parsedResponse.IsSuccess)
                {
                    //successful responses get indexed in the FTS, either the body contents, or the path and inbound links
                    //figure out what to use here
                    if ((parsedResponse is ITextResponse textResponse) && textResponse.HasIndexableText)
                    {
                        //has indexable text, so refresh the FTS with the current content
                        action = FtsIndexAction.RefreshWithCurrent;
                    }
                    else
                    {
                        //nobody text to add to FTS. During post processing, we will use all incoming link text and the URL's path
                        //to create text for the FTS index. for now, don't do anything
                        action = FtsIndexAction.Nothing;
                    }
                }
                else
                {
                    //not a success, so this should not be in the FTS index
                    //IF the previous entry was a success, it will have an FTS entry which needs to be removed
                    if (GeminiParser.IsSuccessStatus(entry.StatusCode))
                    {
                        action = FtsIndexAction.DeletePrevious;
                    }
                    else
                    {
                        //nothing to do
                        action = FtsIndexAction.Nothing;
                    }
                }
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
        if (isContentNewOrChanged)
        {
            entry.IsAvailable = parsedResponse.IsAvailable;
            entry.StatusCode = parsedResponse.StatusCode;
            entry.Meta = parsedResponse.Meta;

            entry.IsBodyIndexed = false;
            entry.IsFeed = false;

            entry.IsBodyTruncated = parsedResponse.IsBodyTruncated;
            entry.BodySize = parsedResponse.BodySize;
            entry.BodyHash = parsedResponse.BodyHash;
            entry.ResponseHash = parsedResponse.Hash;

            entry.Charset = parsedResponse.Charset;
            entry.MimeType = parsedResponse.MimeType;

            entry.OutboundLinks = parsedResponse.Links.Count;

            entry.ContentType = parsedResponse.FormatType;
            entry.DetectedMimeType = parsedResponse.DetectedMimeType;

            entry.Language = parsedResponse.Language;
            entry.DetectedLanguage = null;
            entry.LineCount = null;
            entry.Title = null;

            //extra meta data
            if (parsedResponse is ITextResponse textResponse)
            {
                entry.IsBodyIndexed = textResponse.HasIndexableText;
                entry.DetectedLanguage = textResponse.DetectedLanguage;
                entry.LineCount = textResponse.LineCount;
                entry.Title = textResponse.Title;
                entry.IsFeed = textResponse.IsFeed;
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
                bulkContext.Images.Remove(entry.Image);
                entry.Image = null;
            }

            // A newly reachable ordinary URL may already have inbound links. Its
            // path/image index and popularity therefore need one initial refresh.
            // Proactive metadata has its own tables and does not need FTS/ranking.
            if (!parsedResponse.IsProactiveRequest && (isNewDocument || (!wasAvailable && entry.IsAvailable)))
            {
                IndexWorkType workTypes = IndexWorkType.Popularity;
                if (!entry.IsBodyIndexed)
                {
                    workTypes |= IndexWorkType.File;
                }
                if (entry.Image != null)
                {
                    workTypes |= IndexWorkType.Image;
                }
                QueueIndexWork(parsedResponse.RequestUrl.ID, workTypes);
            }
        }

        //Special-file tables are derived state. Keep them synchronized even when
        //a reimport sees a document body that was already indexed.
        if (parsedResponse.IsProactiveRequest)
        {
            UpdateSpecialFiles(parsedResponse);
        }

        //propogate our if the content has changed to control re-indexing FTS and other operations
        return (action, isContentNewOrChanged);
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

        //see if it already exists
        var entry = bulkContext.RobotsTxts
            .Where(x => (x.Protocol == response!.RequestUrl.Protocol &&
                        x.Domain == response.RequestUrl.Hostname &&
                        x.Port == response.RequestUrl.Port))
            .FirstOrDefault();

        if (isRemove)
        {
            if (entry != null)
            {
                //remove it
                bulkContext.RobotsTxts.Remove(entry);
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
                bulkContext.RobotsTxts.Add(entry);
            }
            else
            {
                entry.Content = response!.BodyText;
            }
        }
    }

    private void UpdateFavicon(ParsedResponse response)
    {
        string emoji = response.BodyText.Trim();
        bool isRemove = !(response.IsSuccess && IsValidFavicon(emoji));

        var entry = bulkContext.Favicons
            .FirstOrDefault(x => x.Protocol == response.RequestUrl.Protocol &&
                                 x.Domain == response.RequestUrl.Hostname &&
                                 x.Port == response.RequestUrl.Port);

        if (isRemove)
        {
            if (entry != null)
            {
                bulkContext.Favicons.Remove(entry);
            }
            return;
        }

        if (entry == null)
        {
            entry = new Favicon(response.RequestUrl);
            bulkContext.Favicons.Add(entry);
        }

        entry.Emoji = emoji;
        entry.SourceUrlID = response.RequestUrl.ID;
    }

    private void UpdateSecurity(ParsedResponse response)
    {
        bool isRemove = !(response.IsSuccess && IsValidSecurity(response.BodyText));

        //see if it already exists
        var entry = bulkContext.SecurityTxts
            .Where(x => (x.Protocol == response.RequestUrl.Protocol &&
                        x.Domain == response.RequestUrl.Hostname &&
                        x.Port == response.RequestUrl.Port))
            .FirstOrDefault();

        if (isRemove)
        {
            if (entry != null)
            {
                bulkContext.SecurityTxts.Remove(entry);
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
                bulkContext.SecurityTxts.Add(entry);
            }
            else
            {
                entry.Content = response.BodyText;
            }
        }
    }

    private bool IsValidFavicon(string contents)
        => FaviconValidator.IsValidEmoji(contents);

    private bool IsValidSecurity(string contents)
        => (contents != null && contents.ToLower().Contains("contact:"));


    private bool IsValidRobotsTxtFile(string? contents)
    {
        if (contents != null)
        {
            var parser = new RobotsTxtParser();
            var robotsTxt = parser.Parse(contents);
            return robotsTxt.HasValidRules;
        }
        return false;
    }

    private void StoreLinks(ParsedResponse response)
    {
        var oldLinks = bulkContext.Links
            .Where(x => x.SourceUrlID == response.RequestUrl.ID)
            .ToDictionary(x => x.TargetUrlID);
        var newLinks = response.Links
            .GroupBy(link => link.Url.ID)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var targetUrlID in oldLinks.Keys.Union(newLinks.Keys))
        {
            bool hadOldLink = oldLinks.TryGetValue(targetUrlID, out var oldLink);
            bool hasNewLink = newLinks.TryGetValue(targetUrlID, out var newLink);
            bool linkTextChanged = hadOldLink != hasNewLink ||
                (hadOldLink && !string.Equals(oldLink!.LinkText, newLink!.LinkText, StringComparison.Ordinal));
            bool externalChanged = hadOldLink != hasNewLink
                ? (hadOldLink ? oldLink!.IsExternal : newLink!.IsExternal)
                : oldLink!.IsExternal != newLink!.IsExternal;

            if (!hadOldLink)
            {
                bulkContext.Links.Add(new DocumentLink
                {
                    SourceUrlID = response.RequestUrl.ID,
                    TargetUrlID = targetUrlID,
                    IsExternal = newLink!.IsExternal,
                    LinkText = newLink.LinkText
                });
            }
            else if (!hasNewLink)
            {
                bulkContext.Links.Remove(oldLink!);
            }
            else if (linkTextChanged || externalChanged)
            {
                oldLink!.IsExternal = newLink!.IsExternal;
                oldLink.LinkText = newLink.LinkText;
            }

            if (linkTextChanged)
            {
                QueueIndexWork(targetUrlID, IndexWorkType.File | IndexWorkType.Image);
            }
            if (externalChanged)
            {
                QueueIndexWork(targetUrlID, IndexWorkType.Popularity);
            }
        }
    }

    private void QueueIndexWork(long urlID, IndexWorkType workTypes)
    {
        var workItem = bulkContext.IndexWorkItems.Find(urlID);
        if (workItem == null)
        {
            bulkContext.IndexWorkItems.Add(new IndexWorkItem
            {
                UrlID = urlID,
                WorkTypes = workTypes
            });
            return;
        }

        workItem.WorkTypes |= workTypes;
    }
}
