using Gemini.Net;
using Kennedy.Data.Models;
using Kennedy.Data.Parsers;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Services;

/// <summary>
/// Single storage entrypoint for crawl responses.
/// Handles URL lifecycle updates and parsed document persistence.
/// </summary>
public sealed class ResponseStore
{
    private readonly IDbContextFactory<KennedyDbContext> _dbFactory;
    private readonly ResponseParser _responseParser;

    public ResponseStore(IDbContextFactory<KennedyDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _responseParser = new ResponseParser();
    }

    public async Task StoreResponseAsync(GeminiResponse response, CancellationToken ct)
    {
        var visitTimeUtc = response.RequestSent ?? DateTime.UtcNow;
        var parsedResponse = _responseParser.Parse(response);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var url = await db.UrlRegistry.SingleOrDefaultAsync(u => u.NormalizedUrl == response.RequestUrl.NormalizedUrl, ct);
        if (url == null)
        {
            url = new UrlRecord(response.RequestUrl.NormalizedUrl)
            {
                FirstSeen = visitTimeUtc,
                Status = UrlStatus.New
            };
            db.UrlRegistry.Add(url);
        }

        ApplyUrlLifecycle(url, response, visitTimeUtc);
        await db.SaveChangesAsync(ct);

        await UpsertDocumentAsync(db, url, parsedResponse, visitTimeUtc, ct);

        await tx.CommitAsync(ct);
    }

    private static void ApplyUrlLifecycle(UrlRecord url, GeminiResponse response, DateTime visitTimeUtc)
    {
        url.LastVisit = visitTimeUtc;
        url.LastStatusCode = response.StatusCode;

        if (GeminiParser.IsSuccessStatus(response.StatusCode))
        {
            url.LastSuccess = visitTimeUtc;
            url.SuccessCount++;
            url.Meta = string.Empty;

            if (!string.IsNullOrEmpty(response.Hash)
                && !string.Equals(url.LastContentHash, response.Hash, StringComparison.Ordinal))
            {
                url.LastContentHash = response.Hash;
                url.LastContentChange = visitTimeUtc;
            }

            url.Status = UrlStatus.Active;
            return;
        }

        if (GeminiParser.ConnectionErrorStatusCode == response.StatusCode)
        {
            url.FailureCount++;
            url.Status = UrlStatus.ConnectionError;
            url.Meta = string.Empty;
            return;
        }

        if (GeminiParser.IsTempFailStatus(response.StatusCode) || GeminiParser.IsPermFailStatus(response.StatusCode))
        {
            url.FailureCount++;
            url.Status = UrlStatus.TemporaryError;
            url.Meta = string.Empty;
            return;
        }

        if (GeminiParser.IsRedirectStatus(response.StatusCode))
        {
            url.LastSuccess = visitTimeUtc;
            url.SuccessCount++;
            url.Meta = response.Meta;
            url.Status = UrlStatus.Redirect;
            return;
        }

        if (GeminiParser.IsInputStatus(response.StatusCode))
        {
            url.LastSuccess = visitTimeUtc;
            url.SuccessCount++;
            url.Meta = response.Meta;
            url.Status = UrlStatus.Interactive;
            return;
        }

        url.Status = UrlStatus.UNKNOWN;
        url.Meta = string.Empty;
    }

    private static async Task UpsertDocumentAsync(
        KennedyDbContext db,
        UrlRecord url,
        ParsedResponse parsedResponse,
        DateTime indexedUtc,
        CancellationToken ct)
    {
        var existing = await db.Documents
            .Include(d => d.Image)
            .SingleOrDefaultAsync(d => d.NormalizedUrl == url.NormalizedUrl, ct);

        if (existing == null)
        {
            existing = new DocumentRecord
            {
                UrlRegistryId = url.Id,
                NormalizedUrl = url.NormalizedUrl,
                CanonicalUrl = url.NormalizedUrl,
                LastIndexedUtc = indexedUtc
            };
            db.Documents.Add(existing);
        }

        // Skip rewriting unchanged payloads to avoid unnecessary FTS churn.
        // We still refresh mutable timeline fields.
        if (existing.ResponseHash == parsedResponse.Hash)
        {
            existing.UrlRegistryId = url.Id;
            existing.LastIndexedUtc = indexedUtc;
            existing.StatusCode = parsedResponse.StatusCode;
            await db.SaveChangesAsync(ct);
            return;
        }

        existing.UrlRegistryId = url.Id;
        existing.CanonicalUrl = url.NormalizedUrl;
        existing.LastIndexedUtc = indexedUtc;

        existing.StatusCode = parsedResponse.StatusCode;
        existing.ContentType = parsedResponse.FormatType;
        existing.MimeType = parsedResponse.MimeType;
        existing.DetectedMimeType = parsedResponse.DetectedMimeType;
        existing.IsBodyTruncated = parsedResponse.IsBodyTruncated;
        existing.BodySize = parsedResponse.BodySize;
        existing.BodyHash = parsedResponse.BodyHash;
        existing.ResponseHash = parsedResponse.Hash;
        existing.OutboundLinks = parsedResponse.Links.Count;

        existing.Language = parsedResponse.Language;
        existing.DetectedLanguage = null;
        existing.LineCount = null;
        existing.Title = null;
        existing.IsFeed = false;

        if (parsedResponse is ITextResponse textResponse)
        {
            existing.IsSearchable = textResponse.HasIndexableText;
            existing.Content = textResponse.IndexableText ?? string.Empty;
            existing.Title = textResponse.Title;
            existing.DetectedLanguage = textResponse.DetectedLanguage;
            existing.LineCount = textResponse.LineCount;
            existing.IsFeed = textResponse.IsFeed;
        }
        else
        {
            existing.IsSearchable = false;
            existing.Content = string.Empty;
        }

        if (parsedResponse is ImageResponse image)
        {
            if (existing.Image == null)
            {
                existing.Image = new DocumentImageRecord
                {
                    Document = existing,
                    Width = image.Width,
                    Height = image.Height,
                    ImageType = image.ImageType,
                    IsTransparent = image.IsTransparent
                };
            }
            else
            {
                existing.Image.Width = image.Width;
                existing.Image.Height = image.Height;
                existing.Image.ImageType = image.ImageType;
                existing.Image.IsTransparent = image.IsTransparent;
            }
        }
        else if (existing.Image != null)
        {
            db.DocumentImages.Remove(existing.Image);
            existing.Image = null;
        }

        await db.SaveChangesAsync(ct);
    }
}
