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
        ApplyUrlComponents(url);
        await db.SaveChangesAsync(ct);

        await UpsertDocumentAsync(db, url, parsedResponse, visitTimeUtc, ct);
        await UpdateLinksAsync(db, url, parsedResponse, ct);

        await tx.CommitAsync(ct);
    }

    private static void ApplyUrlComponents(UrlRecord url)
    {
        if (!Uri.TryCreate(url.NormalizedUrl, UriKind.Absolute, out var parsed))
        {
            return;
        }

        url.Scheme = parsed.Scheme.ToLowerInvariant();
        url.Host = parsed.Host.ToLowerInvariant();
        url.Port = parsed.IsDefaultPort ? 1965 : parsed.Port;
        url.PathAndQuery = string.IsNullOrWhiteSpace(parsed.PathAndQuery) ? "/" : parsed.PathAndQuery;
        url.FileName = Path.GetFileName(parsed.AbsolutePath);
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
        var isIndexableText = parsedResponse is ITextResponse textResponse && textResponse.HasIndexableText;
        if (!isIndexableText)
        {
            var nonTextExisting = await db.Documents
                .Include(d => d.Image)
                .SingleOrDefaultAsync(d => d.NormalizedUrl == url.NormalizedUrl, ct);
            if (nonTextExisting != null)
            {
                if (nonTextExisting.Image != null)
                {
                    db.DocumentImages.Remove(nonTextExisting.Image);
                }

                db.Documents.Remove(nonTextExisting);
                await db.SaveChangesAsync(ct);
            }

            url.IsTextDocument = false;
            url.IsImage = parsedResponse is ImageResponse;
            url.LastMimeType = parsedResponse.MimeType;
            url.LastDetectedMimeType = parsedResponse.DetectedMimeType;
            if (parsedResponse is ImageResponse nonTextImage)
            {
                url.ImageWidth = nonTextImage.Width;
                url.ImageHeight = nonTextImage.Height;
                url.ImageType = nonTextImage.ImageType;
            }
            else
            {
                url.ImageWidth = null;
                url.ImageHeight = null;
                url.ImageType = null;
            }
            return;
        }

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

        if (parsedResponse is ITextResponse textResponse2)
        {
            existing.IsSearchable = textResponse2.HasIndexableText;
            existing.Content = textResponse2.IndexableText ?? string.Empty;
            existing.Title = textResponse2.Title;
            existing.DetectedLanguage = textResponse2.DetectedLanguage;
            existing.LineCount = textResponse2.LineCount;
            existing.IsFeed = textResponse2.IsFeed;
        }
        else
        {
            existing.IsSearchable = false;
            existing.Content = string.Empty;
        }

        await db.SaveChangesAsync(ct);

        url.IsTextDocument = true;
        url.IsImage = parsedResponse is ImageResponse;
        url.LastMimeType = parsedResponse.MimeType;
        url.LastDetectedMimeType = parsedResponse.DetectedMimeType;
        if (parsedResponse is ImageResponse image)
        {
            url.ImageWidth = image.Width;
            url.ImageHeight = image.Height;
            url.ImageType = image.ImageType;
        }
        else
        {
            url.ImageWidth = null;
            url.ImageHeight = null;
            url.ImageType = null;
        }
    }

    /// <summary>
    /// Replaces the complete set of outbound links for <paramref name="sourceUrl"/> with the links
    /// discovered in this response. Creates new UrlRegistry entries for any undiscovered target URLs.
    /// </summary>
    private static async Task UpdateLinksAsync(
        KennedyDbContext db,
        UrlRecord sourceUrl,
        ParsedResponse parsedResponse,
        CancellationToken ct)
    {
        // Full replacement: delete all existing links from this source, then re-insert.
        // This handles the case where previously linked pages are no longer linked.
        var previousLinks = db.UrlLinks.Where(x => x.SourceUrlId == sourceUrl.Id);
        db.UrlLinks.RemoveRange(previousLinks);

        var distinctLinks = parsedResponse.Links.Distinct().ToList();
        var targetUrls = distinctLinks.Select(l => l.Url.NormalizedUrl).Distinct().ToList();

        // Load all already-known target URLs in one batch query.
        var existingTargets = await db.UrlRegistry
            .Where(u => targetUrls.Contains(u.NormalizedUrl))
            .ToDictionaryAsync(u => u.NormalizedUrl, ct);

        // Also check EF's change tracker: another link on the same page may have already staged
        // an Insert for a URL not yet in the database. Without this check we'd try to insert it twice,
        // causing a unique constraint violation on NormalizedUrl.
        var trackedTargets = db.ChangeTracker
            .Entries<UrlRecord>()
            .Where(e => e.State != EntityState.Deleted)
            .Select(e => e.Entity)
            .GroupBy(e => e.NormalizedUrl)
            .ToDictionary(g => g.Key, g => g.First());

        // First pass: create UrlRegistry entries for newly discovered URLs (no SaveChanges yet,
        // so we accumulate all inserts and flush in one round-trip below).
        foreach (var foundLink in distinctLinks)
        {
            if (existingTargets.ContainsKey(foundLink.Url.NormalizedUrl) || trackedTargets.ContainsKey(foundLink.Url.NormalizedUrl))
            {
                continue;
            }

            var target = new UrlRecord(foundLink.Url.NormalizedUrl)
            {
                FirstSeen = parsedResponse.RequestSent ?? DateTime.UtcNow
            };
            ApplyUrlComponents(target);
            db.UrlRegistry.Add(target);
            // Register in trackedTargets so subsequent links on this page don't try to re-insert the same URL.
            trackedTargets[target.NormalizedUrl] = target;
        }

        // Flush new UrlRegistry rows; EF will populate their auto-generated IDs.
        await db.SaveChangesAsync(ct);

        // Second pass: now that all target IDs are known, insert the UrlLink rows.
        foreach (var foundLink in distinctLinks)
        {
            if (!existingTargets.TryGetValue(foundLink.Url.NormalizedUrl, out var target) &&
                !trackedTargets.TryGetValue(foundLink.Url.NormalizedUrl, out target))
            {
                continue;
            }

            db.UrlLinks.Add(new UrlLinkRecord
            {
                SourceUrlId = sourceUrl.Id,
                TargetUrlId = target.Id,
                IsExternal = foundLink.IsExternal,
                LinkText = (foundLink.LinkText ?? string.Empty).Trim()
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
