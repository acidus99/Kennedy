# Crawler Architecture Improvements

Decisions and design notes from reviewing Mercator, Cho & Garcia-Molina, WebFountain, and related papers. These are all planned but not yet implemented unless noted.

---

## 1. Frontier: Mercator Two-Level Design

Replace the static hash-bucket frontier with a heap-based design that decouples politeness enforcement from worker assignment.

**Structure:**
```
CrawlFrontier
├── Heap<ip, NextContactTime>       — when can I contact this IP next?
└── Dict<ip, PerIpQueue>            — which URL to pull when I can?
```

**Worker cycle:**
1. Pop the IP with the smallest `NextContactTime` from the heap (if ≤ now).
2. Pull the highest-priority URL from that IP's per-IP queue.
3. Fetch it.
4. Push the IP back onto the heap with `NextContactTime = now + politenessDelay`.

Any worker can claim any ready IP. Workers are not statically bound to buckets.

---

## 2. IP-Based Politeness Bucketing

**[IMPLEMENTED in Kennedy-1]** Use resolved IP address as the politeness key, not hostname/authority. Gemini uses SNI, so multiple capsules (e.g. all `*.flounder.online` subdomains) share a single IP and must share a politeness slot.

Lookup via `DnsCache.Global.GetLookup(url.Hostname)`. Fall back to hostname string if DNS fails.

Future: politeness key should be `scheme + "://" + resolved_ip` to allow different protocols on the same IP to have separate politeness queues.

---

## 3. Intra-Queue URL Ordering

Within a per-IP queue, multiple capsules may compete for the same IP slot (especially on shared hosts like flounder). Use a composite priority to ensure fairness and content-type preference:

```
priority = (authority_count, content_type_rank, -inlink_score)
```

- **authority_count**: count of URLs from this capsule already enqueued. Acts as a round-robin — capsules with fewer enqueued URLs jump ahead of capsules that dumped 1000 URLs at once.
- **content_type_rank**: 0 for `text/gemini` and `text/*`, 1 for binary. Fetch parseable content first since it produces new links.
- **-inlink_score**: break ties toward capsules with higher `PriorityScore` (inlink count from `UrlLinks`).

The old Kennedy `UrlQueue.GetPriority()` implements `authority_count` + `content_type_rank` within a bucket; the new system applies the same logic at the per-IP queue level.

---

## 4. Per-IP Contact Timing (Politeness Delay)

Base delay: 5 seconds between requests to any single IP.

Increase for known shared-host IPs: if a resolved IP hosts more than N distinct authorities (detected at runtime), apply a longer delay (10–15 seconds) since the physical server is handling more tenants.

DNS TTL: cache entries should expire and be re-resolved. Use the DNS TTL or 30 minutes as a floor, whichever is larger. A stale DNS entry means a capsule might temporarily land in the wrong politeness slot — acceptable, not worth the complexity of dynamic re-bucketing mid-crawl.

---

## 5. UrlRegistry as Frontier

The crawler seeds itself each run from the database rather than from a static seed file:

```sql
SELECT * FROM UrlRegistry
WHERE NextCrawlAt <= UTC_NOW
  AND Status NOT IN ('DenyList', 'ManuallyDisabled')
ORDER BY PriorityScore DESC
LIMIT @batchSize
```

`NextCrawlAt = null` means "do not recrawl" (permanent failures, manually excluded). The heap frontier is loaded from this query at startup and refilled as URLs are discovered during the crawl.

---

## 6. Recrawl Scheduling via NextCrawlAt

Add `NextCrawlAt DateTime?` to `UrlRecord`. Drive all scheduling off this column.

**After a successful fetch:**
```csharp
if (contentChanged)           NextCrawlAt = now + 1 day;    // saw it change: crawl soon
else if (successCount > 5)    NextCrawlAt = now + 14 days;  // stable long-term: slow down
else                          NextCrawlAt = now + 7 days;   // default
```

**After a transient failure:**
```csharp
NextCrawlAt = now + TimeSpan.FromDays(Math.Min(failureCount, 7));  // exponential backoff, cap 7 days
```

**After permanent failure (51 Gone, repeated 4x errors):**
```csharp
NextCrawlAt = null;  // scheduler ignores null — never recrawl
```

**Scheduling insight from Cho & Garcia-Molina:** Uniform recrawl rates maximize *freshness* (fraction of pages that are current right now). Proportional-to-change-frequency rates maximize *age* reduction. For a search engine, freshness is the right metric — so resist the intuition to hammer frequently-changing pages.

Round `NextCrawlAt` into discrete buckets (1 day / 3 day / 7 day / 14 day / null) rather than computing a continuous value per page. Makes the `WHERE NextCrawlAt <= now` query more cache-friendly and easier to reason about budget ("how many pages fall in each bucket?").

Add index: `(NextCrawlAt, Status)`.

---

## 7. URL Priority / Inlink Score

Use inlink count as a proxy for PageRank. Set `PriorityScore` on `UrlRecord` to the count of inbound links from `UrlLinks` where target = this URL. Update incrementally: each time a page is parsed, upsert link records and increment the target's `PriorityScore`.

From the "efficient crawling through URL ordering" paper: inlink count gives 75–80% of the freshness benefit of full PageRank ordering. Full iterative PageRank (OPIC) adds complexity without meaningful gain at Geminispace scale.

---

## 8. URL Deduplication / Seen-URL Cache

The `UNIQUE INDEX on NormalizedUrl` in `UrlRegistry` handles dedup at insert time. No separate Bloom filter needed at Geminispace scale.

Add an in-memory LRU cache (~50K entries) in front of the SQLite lookup for recently-seen URLs. From the URL caching paper: 50K entries gives ~80% hit rate. Avoids the SQLite round-trip for the common case of popular capsules re-linked on every page.

---

## 9. Content Deduplication

Hash the normalized body (strip whitespace, lowercase) before storing `LastContentHash`. If the hash matches the previous crawl, skip re-indexing. Exact dedup via hash is sufficient — near-duplicate shingling is not worth the complexity at Geminispace scale.

---

## 10. Crawler Traps

Cap `UrlRegistry` rows per host at a fixed limit (e.g. 10,000). Any host approaching this limit likely has a dynamic URL space (calendar, session IDs, faceted navigation). Log it, set excess URLs to `Status = ManuallyDisabled`, and flag the host for review.

Apply a depth penalty: URLs with 5+ path segments get lower `PriorityScore` by default at enqueue time.

---

## 11. DNS Cache Sizing

From the URL caching paper: 50K LRU entries gives ~80% hit rate on web-scale hostname sets. Geminispace has maybe 5K–10K distinct hostnames total, so a full in-memory DNS cache requires no eviction.

Respect DNS TTL per entry with a 30-minute floor. Allows IP reassignments to propagate across crawl runs without serving stale mappings indefinitely.
