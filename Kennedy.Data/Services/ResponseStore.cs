using Gemini.Net;
using Kennedy.Data.Models;
using Kennedy.Data.Parsers;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Services;

/// <summary>
/// Single storage entrypoint for crawl responses.
/// Handles URL lifecycle updates and parsed document persistence.
///
/// For bulk WARC ingestion, use <see cref="StoreBatchAsync"/> which processes a list of responses
/// inside a single transaction and DbContext, dramatically reducing SQLite commit overhead.
/// <see cref="StoreResponseAsync"/> remains available for one-off use.
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

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores a single response. Opens its own DbContext and transaction.
    /// Prefer <see cref="StoreBatchAsync"/> for high-throughput ingestion.
    /// </summary>
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
        ApplyUrlMetadata(url, parsedResponse);
        await db.SaveChangesAsync(ct);

        var existingDoc = await db.Documents
            .Include(d => d.Image)
            .SingleOrDefaultAsync(d => d.UrlRegistryId == url.Id, ct);

        var (doc, ftsText) = ApplyDocumentToContext(db, url, parsedResponse, visitTimeUtc, existingDoc);
        long? ftsDeleteId = doc != null && db.Entry(doc).State == EntityState.Deleted ? doc.Id : null;

        await db.SaveChangesAsync(ct);

        if (ftsDeleteId.HasValue)
        {
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM DocumentsFts WHERE rowid = {ftsDeleteId.Value}", ct);
        }
        else if (doc != null && ftsText != null)
        {
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM DocumentsFts WHERE rowid = {doc.Id}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO DocumentsFts(rowid, Title, Content, CanonicalUrl) VALUES ({doc.Id}, {doc.Title}, {ftsText}, {doc.CanonicalUrl})", ct);
        }

        await UpdateLinksAsync(db, url, parsedResponse, ct);

        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Stores a batch of responses in a single SQLite transaction using one shared DbContext.
    /// This is the preferred path for WARC ingestion — it reduces commit overhead from O(N) to O(1).
    ///
    /// Write order respects foreign-key constraints:
    /// 1. UrlRegistry rows for all source URLs (SaveChanges → IDs populated).
    /// 2. Document rows + UrlRegistry rows for newly discovered link targets (SaveChanges → IDs populated).
    /// 3. UrlLink rows, preceded by a raw SQL DELETE of stale links (SaveChanges).
    /// 4. Single COMMIT.
    /// </summary>
    public async Task StoreBatchAsync(IReadOnlyList<GeminiResponse> responses, CancellationToken ct)
    {
        if (responses.Count == 0)
        {
            return;
        }

        // Parse all responses upfront (CPU work outside the transaction).
        var parsed = responses
            .Select(r => (response: r, parsed: _responseParser.Parse(r), visitTime: r.RequestSent ?? DateTime.UtcNow))
            .ToList();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // ── Phase 1: UrlRegistry for source URLs ──────────────────────────────
        //
        // One IN query loads any pre-existing records; everything else is a fresh INSERT.
        // ApplyUrlLifecycle + ApplyUrlComponents + ApplyUrlMetadata run per record,
        // all accumulated in the change tracker before a single SaveChanges.

        var sourceUrls = parsed.Select(p => p.response.RequestUrl.NormalizedUrl).ToList();

        var existingUrlMap = await db.UrlRegistry
            .Where(u => sourceUrls.Contains(u.NormalizedUrl))
            .ToDictionaryAsync(u => u.NormalizedUrl, ct);

        var urlMap = new Dictionary<string, UrlRecord>(parsed.Count);

        foreach (var (response, parsedResponse, visitTime) in parsed)
        {
            var normalizedUrl = response.RequestUrl.NormalizedUrl;

            if (!existingUrlMap.TryGetValue(normalizedUrl, out var url))
            {
                url = new UrlRecord(normalizedUrl) { FirstSeen = visitTime };
                db.UrlRegistry.Add(url);
            }

            ApplyUrlLifecycle(url, response, visitTime);
            ApplyUrlComponents(url);
            ApplyUrlMetadata(url, parsedResponse);
            urlMap[normalizedUrl] = url;
        }

        // Flush all UrlRegistry rows — EF populates auto-generated IDs after this call.
        await db.SaveChangesAsync(ct);

        // ── Phase 2: Documents + UrlRegistry for new link-target URLs ─────────
        //
        // Batch-load existing Documents and existing target UrlRecords in two IN queries.
        // New link targets that don't exist yet are inserted here so their IDs are
        // available when building UrlLink rows in Phase 3.

        var sourceUrlIds = urlMap.Values.Select(u => (long?)u.Id).ToList();
        var existingDocMap = await db.Documents
            .Include(d => d.Image)
            .Where(d => sourceUrlIds.Contains(d.UrlRegistryId))
            .ToDictionaryAsync(d => d.UrlRegistryId!.Value, ct);

        // Collect every unique link target URL across the whole batch.
        var allLinkTargets = new Dictionary<string, FoundLink>();
        foreach (var (_, parsedResponse, _) in parsed)
        {
            foreach (var link in parsedResponse.Links.Distinct())
            {
                allLinkTargets.TryAdd(link.Url.NormalizedUrl, link);
            }
        }

        // Apply document changes to the change tracker (no SaveChanges yet).
        var ftsDeletes = new List<long>();
        var ftsUpserts = new List<(DocumentRecord doc, string text)>();

        foreach (var (response, parsedResponse, visitTime) in parsed)
        {
            var url = urlMap[response.RequestUrl.NormalizedUrl];
            existingDocMap.TryGetValue(url.Id, out var existingDoc);
            var (doc, ftsText) = ApplyDocumentToContext(db, url, parsedResponse, visitTime, existingDoc);
            if (doc == null) continue;
            if (db.Entry(doc).State == EntityState.Deleted)
                ftsDeletes.Add(doc.Id);
            else if (ftsText != null)
                ftsUpserts.Add((doc, ftsText));
        }

        // One IN query to find which link targets are already in the registry.
        var targetUrlStrings = allLinkTargets.Keys.ToList();
        var existingTargetMap = targetUrlStrings.Count > 0
            ? await db.UrlRegistry
                .Where(u => targetUrlStrings.Contains(u.NormalizedUrl))
                .ToDictionaryAsync(u => u.NormalizedUrl, ct)
            : new Dictionary<string, UrlRecord>();

        // Insert UrlRegistry rows for link targets not yet seen.
        // Skip URLs that are also source URLs in this batch (already in urlMap).
        var newTargetMap = new Dictionary<string, UrlRecord>();
        foreach (var (targetUrl, _) in allLinkTargets)
        {
            if (existingTargetMap.ContainsKey(targetUrl) || urlMap.ContainsKey(targetUrl))
            {
                continue;
            }

            var target = new UrlRecord(targetUrl) { FirstSeen = DateTime.UtcNow };
            ApplyUrlComponents(target);
            db.UrlRegistry.Add(target);
            newTargetMap[targetUrl] = target;
        }

        // Flush Documents + new target UrlRegistry rows — IDs populated after this call.
        await db.SaveChangesAsync(ct);

        // FTS management — after SaveChanges so new Documents have their IDs populated.
        if (ftsDeletes.Count > 0)
        {
            var idList = string.Join(",", ftsDeletes);
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM DocumentsFts WHERE rowid IN ({idList})", ct);
        }
        foreach (var (doc, text) in ftsUpserts)
        {
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM DocumentsFts WHERE rowid = {doc.Id}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO DocumentsFts(rowid, Title, Content, CanonicalUrl) VALUES ({doc.Id}, {doc.Title}, {text}, {doc.CanonicalUrl})", ct);
        }

        // ── Phase 3: UrlLinks ──────────────────────────────────────────────────
        //
        // Delete all stale outbound links for every source URL in one raw SQL statement,
        // then insert fresh link rows. IDs for all targets are now available.

        var sourceIdsCsv = string.Join(",", urlMap.Values.Select(u => u.Id));
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM UrlLinks WHERE SourceUrlId IN ({sourceIdsCsv})", ct);

        // Build a unified lookup: source URLs + existing targets + newly created targets.
        // A source URL can also appear as a link target, so urlMap is included.
        var combinedTargetMap = new Dictionary<string, UrlRecord>(
            existingTargetMap.Count + newTargetMap.Count + urlMap.Count);
        foreach (var kv in existingTargetMap) combinedTargetMap.TryAdd(kv.Key, kv.Value);
        foreach (var kv in newTargetMap)      combinedTargetMap.TryAdd(kv.Key, kv.Value);
        foreach (var kv in urlMap)            combinedTargetMap.TryAdd(kv.Key, kv.Value);

        foreach (var (response, parsedResponse, _) in parsed)
        {
            var sourceUrl = urlMap[response.RequestUrl.NormalizedUrl];
            foreach (var link in parsedResponse.Links.Distinct())
            {
                if (!combinedTargetMap.TryGetValue(link.Url.NormalizedUrl, out var target))
                {
                    continue;
                }

                db.UrlLinks.Add(new UrlLinkRecord
                {
                    SourceUrlId = sourceUrl.Id,
                    TargetUrlId = target.Id,
                    IsExternal = link.IsExternal,
                    LinkText = (link.LinkText ?? string.Empty).Trim()
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

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

    /// <summary>
    /// Updates the URL's content-type metadata fields from the parsed response.
    /// Separated from <see cref="ApplyUrlLifecycle"/> so both single-record and batch paths
    /// can set these fields before the first SaveChanges, avoiding a second round-trip.
    /// </summary>
    private static void ApplyUrlMetadata(UrlRecord url, ParsedResponse parsedResponse)
    {
        url.LastMimeType = parsedResponse.MimeType;
        url.LastDetectedMimeType = parsedResponse.DetectedMimeType;

        if (parsedResponse is ImageResponse image)
        {
            url.IsImage = true;
            url.IsTextDocument = false;
            url.ImageWidth = image.Width;
            url.ImageHeight = image.Height;
            url.ImageType = image.ImageType;
        }
        else if (parsedResponse is ITextResponse textResponse && textResponse.HasIndexableText)
        {
            url.IsTextDocument = true;
            url.IsImage = false;
            url.ImageWidth = null;
            url.ImageHeight = null;
            url.ImageType = null;
        }
        else
        {
            url.IsTextDocument = false;
            url.IsImage = false;
            url.ImageWidth = null;
            url.ImageHeight = null;
            url.ImageType = null;
        }
    }

    /// <summary>
    /// Applies document changes to the EF change tracker without calling SaveChanges.
    /// Used by both the single-record and batch paths so that callers control when the flush happens.
    /// Returns the affected DocumentRecord and the FTS text to index (null = no FTS action needed).
    /// A non-null doc whose EF state is Deleted signals the caller to remove it from DocumentsFts.
    /// </summary>
    private static (DocumentRecord? doc, string? ftsText) ApplyDocumentToContext(
        KennedyDbContext db,
        UrlRecord url,
        ParsedResponse parsedResponse,
        DateTime indexedUtc,
        DocumentRecord? existing)
    {
        var isIndexableText = parsedResponse is ITextResponse textResponse && textResponse.HasIndexableText;

        if (!isIndexableText)
        {
            if (existing != null)
            {
                if (existing.Image != null)
                {
                    db.DocumentImages.Remove(existing.Image);
                }
                db.Documents.Remove(existing);
                return (existing, null);
            }
            return (null, null);
        }

        if (existing == null)
        {
            existing = new DocumentRecord
            {
                UrlRegistryId = url.Id,
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
            existing.Host = url.Host;
            existing.LastMimeType = url.LastMimeType;
            return (existing, null);
        }

        existing.UrlRegistryId = url.Id;
        existing.CanonicalUrl = url.NormalizedUrl;
        existing.Host = url.Host;
        existing.LastMimeType = url.LastMimeType;
        existing.LastIndexedUtc = indexedUtc;
        existing.StatusCode = parsedResponse.StatusCode;
        existing.ContentType = parsedResponse.FormatType;
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

        string ftsContent = string.Empty;

        if (parsedResponse is ITextResponse textResponse2)
        {
            existing.Title = textResponse2.Title;
            existing.DetectedLanguage = textResponse2.DetectedLanguage;
            existing.LineCount = textResponse2.LineCount;
            existing.IsFeed = textResponse2.IsFeed;
            ftsContent = textResponse2.IndexableText ?? string.Empty;
        }

        return (existing, ftsContent);
    }

    /// <summary>
    /// Replaces the outbound links for <paramref name="sourceUrl"/> in the single-record path.
    /// Uses raw SQL DELETE (not EF RemoveRange) to avoid loading and individually deleting each row.
    /// </summary>
    private static async Task UpdateLinksAsync(
        KennedyDbContext db,
        UrlRecord sourceUrl,
        ParsedResponse parsedResponse,
        CancellationToken ct)
    {
        // Raw SQL is faster than EF's RemoveRange, which would load every row then issue
        // individual DELETE statements.
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM UrlLinks WHERE SourceUrlId = {sourceUrl.Id}", ct);

        var distinctLinks = parsedResponse.Links.Distinct().ToList();
        var targetUrls = distinctLinks.Select(l => l.Url.NormalizedUrl).Distinct().ToList();

        // Load all already-known target URLs in one batch query.
        var existingTargets = await db.UrlRegistry
            .Where(u => targetUrls.Contains(u.NormalizedUrl))
            .ToDictionaryAsync(u => u.NormalizedUrl, ct);

        // Also check the change tracker so we don't try to insert a URL that was already
        // staged by a previous step in this same transaction (e.g. two links to the same new URL).
        var trackedTargets = db.ChangeTracker
            .Entries<UrlRecord>()
            .Where(e => e.State != EntityState.Deleted)
            .Select(e => e.Entity)
            .GroupBy(e => e.NormalizedUrl)
            .ToDictionary(g => g.Key, g => g.First());

        // First pass: create UrlRegistry entries for newly discovered URLs.
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
            trackedTargets[target.NormalizedUrl] = target;
        }

        // Flush new UrlRegistry rows so their IDs are available for the link records.
        await db.SaveChangesAsync(ct);

        // Second pass: insert UrlLink rows now that all target IDs are known.
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
