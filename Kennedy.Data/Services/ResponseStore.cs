using Gemini.Net;
using Kennedy.Data.Models;
using Kennedy.Data.Parsers;
using Kennedy.Data.RobotsTxt;
using Kennedy.Data.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public bool ForceReparse { get; set; }
    public long ParsedResponses { get; private set; }
    public long UnchangedResponses { get; private set; }
    public long RepairedDocuments { get; private set; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores a single response. Opens its own DbContext and transaction.
    /// Prefer <see cref="StoreBatchAsync"/> for high-throughput ingestion.
    /// </summary>
    public Task StoreResponseAsync(GeminiResponse response, CancellationToken ct)
        => StoreBatchAsync([response], ct);

    private static bool IsTransient(GeminiResponse response)
        => response.IsConnectionError || response.IsTempFail;

    public async Task StoreBatchAsync(IReadOnlyList<GeminiResponse> responses, CancellationToken ct)
    {
        if (responses.Count == 0) return;
        var groups = responses.GroupBy(r => r.RequestUrl.NormalizedUrl)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.RequestSent ?? DateTime.MinValue).ToList());
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        await db.ApplyPerformancePragmasAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var urls = groups.Keys.ToList();
        var urlMap = await db.UrlRegistry.Where(u => urls.Contains(u.NormalizedUrl)).ToDictionaryAsync(u => u.NormalizedUrl, ct);
        // Check replay and repair requirements before doing expensive body parsing.
        var knownIds = urlMap.Values.Select(u => u.Id).ToArray();
        var docs = await db.Documents.Where(d => d.UrlRegistryId != null && knownIds.Contains(d.UrlRegistryId.Value))
            .ToDictionaryAsync(d => d.UrlRegistryId!.Value, ct);
        var images = await db.Images.Where(i => knownIds.Contains(i.UrlRegistryId))
            .ToDictionaryAsync(i => i.UrlRegistryId, ct);
        var existingFts = new HashSet<long>();
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = tx.GetDbTransaction();
            command.CommandText = $"SELECT rowid FROM DocumentsFts WHERE rowid IN ({string.Join(',', docs.Values.Select(d => d.Id).DefaultIfEmpty(-1))});";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) existingFts.Add(reader.GetInt64(0));
        }
        var current = new List<(UrlRecord url, ParsedResponse parsed, DateTime time)>();
        var dirty = new HashSet<long>();
        var changedPayloads = new HashSet<string>();
        foreach (var (key, visits) in groups)
        {
            if (!urlMap.TryGetValue(key, out var url))
            {
                url = new UrlRecord(key) { FirstSeen = visits[0].RequestSent ?? DateTime.UtcNow };
                db.UrlRegistry.Add(url);
                urlMap[key] = url;
            }
            var policyStatus = url.Status;
            foreach (var response in visits)
            {
                var time = response.RequestSent ?? DateTime.UtcNow;
                if (time < url.FirstSeen) url.FirstSeen = time;
                if (!url.LastVisit.HasValue || time > url.LastVisit)
                    ApplyUrlLifecycle(url, response, time);
            }
            if (policyStatus is >= UrlStatus.ExcludedByRobots and <= UrlStatus.ManuallyDisabled) url.Status = policyStatus;
            ApplyUrlComponents(url);
            // Temporary failures update attempt information only. An older successful
            // response may still supply last-good content, even if seen afterwards.
            var candidate = visits.LastOrDefault(r => !IsTransient(r));
            if (candidate == null) continue;
            var captured = candidate.RequestSent ?? DateTime.UtcNow;
            if (url.LastIndexedVisit.HasValue && ((captured < url.LastIndexedVisit && url.LastIndexedHash != candidate.Hash) ||
                (captured == url.LastIndexedVisit && url.LastIndexedHash != null && url.LastIndexedHash != candidate.Hash))) continue;
            if (url.LastIndexedVisit.HasValue && captured < url.LastIndexedVisit) captured=url.LastIndexedVisit.Value;
            bool samePayload = url.LastIndexedHash == candidate.Hash;
            docs.TryGetValue(url.Id, out var previousDocument);
            bool repairFts = previousDocument != null && !existingFts.Contains(previousDocument.Id);
            bool suppressed = url.Status is >= UrlStatus.ExcludedByRobots and <= UrlStatus.ManuallyDisabled;
            bool missingText = !suppressed && previousDocument == null && candidate.HasBody &&
                !UrlUtility.IsProactiveUrl(candidate.RequestUrl.Url) &&
                url.LastDetectedMimeType is "text/gemini" or "text/plain";
            bool missingImage = !suppressed && candidate.HasBody &&
                url.LastDetectedMimeType?.StartsWith("image/", StringComparison.Ordinal) == true && !images.ContainsKey(url.Id);
            bool needsPolicyRemoval = suppressed && (previousDocument != null || images.ContainsKey(url.Id));
            bool mismatchedDocument = previousDocument != null && previousDocument.ResponseHash != candidate.Hash;
            if (!ForceReparse && samePayload && !repairFts && !missingText && !missingImage &&
                !needsPolicyRemoval && !mismatchedDocument && !UrlUtility.IsProactiveUrl(candidate.RequestUrl.Url))
            {
                url.LastIndexedVisit = captured;
                if (previousDocument != null) previousDocument.LastIndexedUtc = captured;
                if (images.TryGetValue(url.Id, out var previousImage)) previousImage.LastIndexedUtc = captured;
                UnchangedResponses++;
                continue;
            }
            if (!samePayload) changedPayloads.Add(key);
            var parsed = _responseParser.Parse(candidate);
            ParsedResponses++;
            if (repairFts) RepairedDocuments++;
            url.LastIndexedVisit = captured;
            url.LastIndexedHash = candidate.Hash;
            ApplyUrlMetadata(url, parsed);
            if (candidate.IsSuccess)
            {
                url.LastSuccess = !url.LastSuccess.HasValue || captured > url.LastSuccess ? captured : url.LastSuccess;
                url.BodySize = candidate.BodySize;
                url.IsBodyTruncated = candidate.IsBodyTruncated;
            }
            current.Add((url, parsed, captured));
        }
        await db.SaveChangesAsync(ct);
        if (current.Count == 0) { await tx.CommitAsync(ct); return; }
        var hosts = current.Select(x => x.url.Host).Distinct().ToList();
        var policies = await db.RobotsTxts.Where(r => hosts.Contains(r.Host)).ToListAsync(ct);
        var policyChangedSources = new HashSet<long>();
        var rules = policies.ToDictionary(r => (r.Scheme,r.Host,r.Port), r => new RobotsTxtParser().Parse(r.Content));
        foreach (var (url, parsed, _) in current)
        {
            bool excluded = rules.TryGetValue((url.Scheme,url.Host,url.Port), out var robots) &&
                !robots.IsPathAllowed("indexer", parsed.RequestUrl.Path);
            if(excluded!=url.IsRobotsExcluded) policyChangedSources.Add(url.Id);
            url.IsRobotsExcluded=excluded;
        }
        var sourceIds = current.Select(x => x.url.Id).ToList();
        var ftsDeletes = new List<long>();
        var ftsUpserts = new List<(DocumentRecord doc, string text)>();
        foreach (var (url, parsed, captured) in current)
        {
            UpdateSpecialFiles(db, url, parsed);
            if (UrlUtility.IsRobotsUrl(parsed.RequestUrl.Url))
                await QueueFileIndexWorkAsync(db, [url.Id], ct, IndexWorkType.RobotsPolicy);
            docs.TryGetValue(url.Id, out var existing);
            images.TryGetValue(url.Id, out var existingImage);
            var (doc, text) = ApplyDocumentToContext(db, url, parsed, captured, existing, existingImage,
                existing != null && !existingFts.Contains(existing.Id));
            if (doc != null && db.Entry(doc).State == EntityState.Deleted) ftsDeletes.Add(doc.Id);
            else if (doc != null && text != null) ftsUpserts.Add((doc, text));
            if (!parsed.IsProactiveRequest && changedPayloads.Contains(url.NormalizedUrl)) dirty.Add(url.Id);
        }
        var targets = current.SelectMany(p => p.parsed.Links).Select(l => l.Url.NormalizedUrl).Distinct().ToList();
        var targetMap = await db.UrlRegistry.Where(u => targets.Contains(u.NormalizedUrl)).ToDictionaryAsync(u => u.NormalizedUrl, ct);
        foreach (var (key, value) in urlMap) targetMap.TryAdd(key, value);
        foreach (var (url, parsed, captured) in current)
        foreach (var link in parsed.Links)
        {
            if (targetMap.ContainsKey(link.Url.NormalizedUrl)) continue;
            var target = new UrlRecord(link.Url.NormalizedUrl) { FirstSeen = captured };
            ApplyUrlComponents(target);
            db.UrlRegistry.Add(target);
            targetMap[target.NormalizedUrl] = target;
        }
        await db.SaveChangesAsync(ct);
        // Reuse prepared commands across all FTS changes in this transaction.
        await using (var delete = db.Database.GetDbConnection().CreateCommand())
        await using (var insert = db.Database.GetDbConnection().CreateCommand())
        {
            delete.Transaction = insert.Transaction = tx.GetDbTransaction();
            delete.CommandText = "DELETE FROM DocumentsFts WHERE rowid=$id";
            var deleteId = delete.CreateParameter(); deleteId.ParameterName="$id"; deleteId.Value=0L; delete.Parameters.Add(deleteId);
            insert.CommandText = "INSERT INTO DocumentsFts(rowid,Title,Content,CanonicalUrl) VALUES($id,$title,$text,$url)";
            foreach (string name in new[] { "$id", "$title", "$text", "$url" })
            {
                var parameter=insert.CreateParameter(); parameter.ParameterName=name; parameter.Value=""; insert.Parameters.Add(parameter);
            }
            delete.Prepare(); insert.Prepare();
            foreach (var id in ftsDeletes)
            {
                deleteId.Value=id; await delete.ExecuteNonQueryAsync(ct);
            }
            foreach (var (doc, text) in ftsUpserts)
            {
                deleteId.Value=doc.Id; await delete.ExecuteNonQueryAsync(ct);
                insert.Parameters["$id"].Value=doc.Id;
                insert.Parameters["$title"].Value=(object?)doc.Title ?? DBNull.Value;
                insert.Parameters["$text"].Value=text;
                insert.Parameters["$url"].Value=doc.CanonicalUrl;
                await insert.ExecuteNonQueryAsync(ct);
            }
        }
        var previous = await db.UrlLinks.AsNoTracking().Where(l => sourceIds.Contains(l.SourceUrlId)).ToListAsync(ct);
        foreach (var link in previous.Where(l=>policyChangedSources.Contains(l.SourceUrlId))) dirty.Add(link.TargetUrlId);
        var previousBySource = previous.ToLookup(l => l.SourceUrlId);
        foreach (var (url, parsed, _) in current)
        {
            var oldLinks = previousBySource[url.Id].GroupBy(l => l.TargetUrlId)
                .ToDictionary(g => g.Key, g => (g.First().IsExternal, g.First().LinkText));
            var newLinks = (url.Status is >= UrlStatus.ExcludedByRobots and <= UrlStatus.ManuallyDisabled ? Enumerable.Empty<FoundLink>() : parsed.Links).GroupBy(l => targetMap[l.Url.NormalizedUrl].Id)
                .ToDictionary(g => g.Key, g => (g.First().IsExternal, LinkText: (g.First().LinkText ?? "").Trim()));
            if (oldLinks.Count == newLinks.Count && oldLinks.All(kv => newLinks.TryGetValue(kv.Key, out var link) && link == kv.Value)) continue;
            foreach (long id in oldLinks.Keys.Union(newLinks.Keys)) dirty.Add(id);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM UrlLinks WHERE SourceUrlId={url.Id}", ct);
            foreach (var (id, link) in newLinks)
                db.UrlLinks.Add(new UrlLinkRecord { SourceUrlId=url.Id, TargetUrlId=id, IsExternal=link.IsExternal, LinkText=link.LinkText });
        }
        await QueueFileIndexWorkAsync(db, dirty, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // Kept for existing callers; a full import now has no time-based retention cutoff.
    public Task StoreRegistryOnlyBatchAsync(IReadOnlyList<GeminiResponse> responses, CancellationToken ct)
        => StoreBatchAsync(responses, ct);

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
    }

    private static void ApplyUrlLifecycle(UrlRecord url, GeminiResponse response, DateTime visitTimeUtc)
    {
        var policyStatus = url.Status;
        url.LastVisit = visitTimeUtc;
        url.LastStatusCode = response.StatusCode;

        if (GeminiParser.IsSuccessStatus(response.StatusCode))
        {
            url.LastSuccess = visitTimeUtc;
            url.SuccessCount++;
            url.Meta = response.Meta;

            if (!string.IsNullOrEmpty(response.Hash)
                && !string.Equals(url.LastContentHash, response.Hash, StringComparison.Ordinal))
            {
                url.LastContentHash = response.Hash;
                url.LastContentChange = visitTimeUtc;
            }

            url.Status = policyStatus is >= UrlStatus.DenyList and <= UrlStatus.ManuallyDisabled ? policyStatus : UrlStatus.Active;
            return;
        }

        if (GeminiParser.ConnectionErrorStatusCode == response.StatusCode)
        {
            url.FailureCount++;
            url.Status = UrlStatus.ConnectionError;
            url.Meta = response.Meta;
            return;
        }

        if (GeminiParser.IsTempFailStatus(response.StatusCode) || GeminiParser.IsPermFailStatus(response.StatusCode))
        {
            url.FailureCount++;
            url.Status = response.StatusCode == 52 ? UrlStatus.Gone : response.IsPermFail ? UrlStatus.PermanentError : UrlStatus.TemporaryError;
            url.Meta = response.Meta;
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
        url.Meta = response.Meta;
    }

    /// <summary>
    /// Updates the URL's content-type metadata fields from the parsed response.
    /// Separated from <see cref="ApplyUrlLifecycle"/> so both single-record and batch paths
    /// can set these fields before the first SaveChanges, avoiding a second round-trip.
    /// </summary>
    private static void ApplyUrlMetadata(UrlRecord url, ParsedResponse parsedResponse)
    {
        // Only an applied success supplies searchable file metadata. Clear it on
        // redirects/removals so a later timeout cannot revive a former file.
        url.LastMimeType = parsedResponse.IsSuccess ? parsedResponse.MimeType : null;
        url.LastDetectedMimeType = parsedResponse.IsSuccess ? parsedResponse.DetectedMimeType : null;

        // UrlRegistry intentionally stores URL lifecycle and protocol metadata only.
    }

    internal static async Task QueueFileIndexWorkAsync(
        KennedyDbContext db,
        IEnumerable<long> urlIds,
        CancellationToken ct, IndexWorkType workType = IndexWorkType.FileSearch)
    {
        // Accessing Local runs DetectChanges. Do it once, not once per URL.
        var pendingItems = db.IndexWorkItems.Local.ToDictionary(item => item.UrlRegistryId);
        foreach (var batch in urlIds.Distinct().Chunk(900))
        {
            var existingItems = await db.IndexWorkItems
                .Where(item => batch.Contains(item.UrlRegistryId))
                .ToDictionaryAsync(item => item.UrlRegistryId, ct);

            foreach (var urlId in batch)
            {
                if (pendingItems.TryGetValue(urlId, out var pending)) { pending.WorkTypes |= workType; continue; }
                if (existingItems.TryGetValue(urlId, out var item))
                {
                    item.WorkTypes |= workType;
                }
                else
                {
                    var added = new IndexWorkItem
                    {
                        UrlRegistryId = urlId,
                        WorkTypes = workType
                    };
                    db.IndexWorkItems.Add(added);
                    pendingItems[urlId] = added;
                }
            }
        }
    }

    private static void UpdateSpecialFiles(KennedyDbContext db, UrlRecord url, ParsedResponse response)
    {
        if (UrlUtility.IsFaviconUrl(response.RequestUrl.Url))
        {
            UpdateFavicon(db, url, response);
        }
        else if (UrlUtility.IsRobotsUrl(response.RequestUrl.Url))
        {
            UpdateRobotsTxt(db, url, response);
        }
        else if (UrlUtility.IsSecurityUrl(response.RequestUrl.Url))
        {
            UpdateSecurityTxt(db, url, response);
        }
    }

    private static void UpdateFavicon(KennedyDbContext db, UrlRecord url, ParsedResponse response)
    {
        if (!UrlUtility.IsFaviconUrl(response.RequestUrl.Url))
        {
            return;
        }

        string emoji = response.BodyText?.Trim() ?? string.Empty;
        var favicon = db.Favicons.SingleOrDefault(x =>
            x.Scheme == url.Scheme && x.Host == url.Host && x.Port == url.Port);

        if (!GeminiParser.IsSuccessStatus(response.StatusCode) || !FaviconValidator.IsValidEmoji(emoji))
        {
            if (favicon != null)
            {
                db.Favicons.Remove(favicon);
            }
            return;
        }

        if (favicon == null)
        {
            favicon = new FaviconRecord
            {
                Scheme = url.Scheme,
                Host = url.Host,
                Port = url.Port
            };
            db.Favicons.Add(favicon);
        }

        favicon.Emoji = emoji;
        favicon.SourceUrlRegistryId = url.Id;
    }

    private static void UpdateRobotsTxt(KennedyDbContext db, UrlRecord url, ParsedResponse response)
    {
        string content = response.BodyText ?? string.Empty;
        bool isValid = GeminiParser.IsSuccessStatus(response.StatusCode) &&
            new RobotsTxtParser().Parse(content).HasValidRules;
        var record = db.RobotsTxts.SingleOrDefault(x =>
            x.Scheme == url.Scheme && x.Host == url.Host && x.Port == url.Port);

        if (!isValid)
        {
            if (record != null) db.RobotsTxts.Remove(record);
            return;
        }

        if (record == null)
        {
            record = new RobotsTxtRecord { Scheme = url.Scheme, Host = url.Host, Port = url.Port };
            db.RobotsTxts.Add(record);
        }
        record.Content = content;
        record.SourceUrlRegistryId = url.Id;
    }

    private static void UpdateSecurityTxt(KennedyDbContext db, UrlRecord url, ParsedResponse response)
    {
        string content = response.BodyText ?? string.Empty;
        bool isValid = GeminiParser.IsSuccessStatus(response.StatusCode) &&
            content.Contains("contact:", StringComparison.OrdinalIgnoreCase);
        var record = db.SecurityTxts.SingleOrDefault(x =>
            x.Scheme == url.Scheme && x.Host == url.Host && x.Port == url.Port);

        if (!isValid)
        {
            if (record != null) db.SecurityTxts.Remove(record);
            return;
        }

        if (record == null)
        {
            record = new SecurityTxtRecord { Scheme = url.Scheme, Host = url.Host, Port = url.Port };
            db.SecurityTxts.Add(record);
        }
        record.Content = content;
        record.SourceUrlRegistryId = url.Id;
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
        DateTime visitTimeUtc,
        DocumentRecord? existing, DocumentImageRecord? existingImage, bool repairFts = false)
    {
        var suppressed = url.Status is >= UrlStatus.ExcludedByRobots and <= UrlStatus.ManuallyDisabled;
        var isIndexableText = !suppressed && parsedResponse is ITextResponse textResponse && textResponse.HasIndexableText;

        if (!isIndexableText)
        {
            if (existing != null)
            {
                // Older WARC: don't remove a more recently indexed Document.
                if (visitTimeUtc < existing.LastIndexedUtc)
                    return (null, null);

                db.Documents.Remove(existing);
            }

            if (!suppressed && parsedResponse is ImageResponse imageResponse)
            {
                var image = existingImage;
                if (image == null)
                {
                    image = new DocumentImageRecord { UrlRegistryId = url.Id };
                    db.Images.Add(image);
                }
                else if (visitTimeUtc < image.LastIndexedUtc)
                {
                    return (null, null); // older WARC, don't overwrite image metadata
                }

                image.Width = imageResponse.Width;
                image.Height = imageResponse.Height;
                image.ImageType = imageResponse.ImageType;
                image.IsTransparent = imageResponse.IsTransparent;
                image.LastIndexedUtc = visitTimeUtc;
            }
            else
            {
                if (existingImage != null && visitTimeUtc > existingImage.LastIndexedUtc)
                    db.Images.Remove(existingImage);
            }

            return (existing, null);
        }

        if (existingImage != null) db.Images.Remove(existingImage);

        // Indexable text path.
        if (existing == null)
        {
            existing = new DocumentRecord
            {
                UrlRegistryId = url.Id,
                CanonicalUrl = url.NormalizedUrl,
                LastIndexedUtc = visitTimeUtc
            };
            db.Documents.Add(existing);
        }
        else if (visitTimeUtc < existing.LastIndexedUtc)
        {
            // Older WARC: don't overwrite a more recently indexed Document.
            return (null, null);
        }

        // Skip rewriting unchanged payloads to avoid unnecessary FTS churn.
        // We still refresh mutable timeline fields.
        bool unchanged = existing.ResponseHash == parsedResponse.Hash && !repairFts;

        existing.UrlRegistryId = url.Id;
        existing.CanonicalUrl = url.NormalizedUrl;
        existing.Host = url.Host;
        existing.LastMimeType = url.LastMimeType;
        existing.LastIndexedUtc = visitTimeUtc;
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

        return (existing, unchanged ? null : ftsContent);
    }

}
