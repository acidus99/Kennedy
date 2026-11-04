# Kennedy 2.0 — Agent Session Brief

This document is written for Claude Code. Read it at the start of every session to recover full context quickly. Update it at the end of every session.

---

## What Kennedy Is

Kennedy 2.0 is a Gemini search engine and archiving system. It crawls Gemini capsules, stores responses in WARC archives, and builds a searchable SQLite/FTS5 index.

Primary repo: `/Users/billy/Code/Kennedy` (branch: `kennedy2`)
Old reference codebase (do NOT modify): `/Users/billy/Code/Kennedy-1`
Historical WARCs (136 files, ~700 GB): `/Volumes/WARC-BACKUP/WARCs/` (network drive)
Live database: `/Users/billy/kennedy-capsule/crawl-data/kennedy2.db`
Active crawler output: `/Users/billy/kennedy-capsule/crawler-out/warcs/`

---

All work stays on `kennedy2`. Use the configured author and committer identity and ordinary change descriptions in commit messages.

## Solution Structure

| Project | Role |
|---|---|
| `Kennedy.Data` | EF Core models, DbContext, ResponseStore, parsers |
| `Kennedy.Search` | Read-only search service (SqliteSearchService) |
| `Kennedy.Warc` | WARC reading/writing |
| `Kennedy.Crawler` | Crawler executable (new, ported from Kennedy-1) |
| `Indexer` | WARC-to-database indexer executable |
| `Server` | Web server (search frontend) |
| `Kennedy.Tests` | Tests |
| `Gemini.Net` | External dep at `../../Gemini.Net/src/Gemini.Net.csproj` |

All projects target .NET 8.

---

## Key Architectural Principle

**URL ≠ Document.** `UrlRegistry` tracks crawl lifecycle (status, visit times, failure counts) for every URL ever seen. `Documents` holds the current indexable content for URLs that returned successful text responses. A URL can exist in `UrlRegistry` with no row in `Documents` (connection error, 51 Gone, redirect, binary, etc.).

**`Status = Active` is the liveness indicator.** Set in `ApplyUrlLifecycle` when `LastStatusCode = 20`. No separate `IsAlive` bool is needed. This covers: text (has a `Documents` row), images (has an `Images` row), binary/PDF (UrlRegistry only, Status = Active).

---

## Database Schema (current)

### UrlRegistry (`Kennedy.Data/Models/UrlRecord.cs`)
Core fields: `Id`, `NormalizedUrl`, `Scheme`, `Host`, `Port`, `FirstSeen`, `LastVisit`, `LastSuccess`, `LastContentChange`, `LastContentHash`, `LastStatusCode`, `Status` (enum), `SuccessCount`, `FailureCount`, `PriorityScore`, `Meta`, `LastMimeType`, `LastDetectedMimeType`

`UrlStatus` enum: `New=0, Active=1, TemporaryError=2, ConnectionError=3, PermanentError=4, Gone=5, Redirect=6, ExcludedByRobots=7, DenyList=8, LowValueSuppressed=9, RemovedByOwnerRequest=10, ManuallyDisabled=11, Interactive=12, UNKNOWN=99`

### Documents (`Kennedy.Data/Models/DocumentRecord.cs`)
`Id`, `UrlRegistryId` (FK), `CanonicalUrl`, `Host`, `LastMimeType`, `Title`, `StatusCode`, `BodyHash`, `ResponseHash`, `LastIndexedUtc`, `IsBodyTruncated`, `BodySize`, `OutboundLinks`, `IsFeed`, `LineCount`, `Language`, `DetectedLanguage`, `ContentType`

`ResponseHash` covers status+meta+body. When it matches on re-ingestion, only timeline fields are updated — FTS is NOT rewritten (avoids churn).

### Images (`Kennedy.Data/Models/DocumentImageRecord.cs`)
`UrlRegistryId` (shared PK/FK), `Width`, `Height`, `ImageType`, `IsTransparent`, `LastIndexedUtc`

`LastIndexedUtc` was added to support out-of-order WARC processing guard (older WARCs don't overwrite newer image metadata).

### UrlLinks (`Kennedy.Data/Models/UrlLinkRecord.cs`)
`Id`, `SourceUrlId`, `TargetUrlId`, `IsExternal`, `LinkText`

### FTS5 Tables
- `DocumentsFts` — full-text search over `Title`, `Content`, `CanonicalUrl`
- `UrlSearch` — trigram index for `inurl:` queries (populated by `FileSearchFtsRebuilder`)

---

## Key Service: ResponseStore (`Kennedy.Data/Services/ResponseStore.cs`)

`StoreResponseAsync` delegates to `StoreBatchAsync`, so single and bulk writes share the same rules. Batches group repeated source URLs, update attempt state monotonically, and apply the latest non-transient payload.

`LastIndexedVisit` and `LastIndexedHash` on UrlRegistry retain a watermark even after a document is removed. They prevent an older successful capture from undoing a newer permanent failure, redirect, or access restriction. Temporary failures preserve the last usable payload. Identical replay does not inflate visit counters; missing FTS entries can be reconstructed from matching WARC content.

`IsRobotsExcluded` hides retained payloads without deleting them. SearchPolicyUpdater consumes durable robots work; lifting restrictions restores visibility and refreshes affected file-link text. FileSearchFtsRebuilder consumes dirty file work. Manual policy states also suppress public search.

`StoreRegistryOnlyBatchAsync` remains a compatibility alias to the full batch writer. Bootstrap no longer separates registry and content or applies an age cutoff.

---

## Key Service: SqliteSearchService (`Kennedy.Search/Services/SqliteSearchService.cs`)

Read-only SQLite queries combine DocumentsFts and non-text FilesFts candidates. Text results retain BM25 ordering; snippets are generated only after pagination. Image results retain the rewrite's recency ordering. Filters use shared status/policy checks, filename-extension `filetype:` semantics, host-scoped `site:`, title matches, and URL-path/query substring matches. Non-default capsule ports work with host-scoped search.

WAL mode is already enabled via `KennedyDbContext.ApplyPerformancePragmasAsync()` (journal_mode=WAL, synchronous=NORMAL, 64MB cache). FTS `optimize` writes don't block reads; only VACUUM blocks everything.

---

## Crawler (`Kennedy.Crawler/`)

Ported from Kennedy-1. Keep Kennedy-1 untouched — it is the reference original.

Key files:
- `Program.cs` — entry point, takes `[config-dir] [output-dir]` args
- `CrawlerOptions.cs` — static config paths
- `Crawling/WebCrawler.cs` — main crawl orchestration, Ctrl+C handler, stop-file quit
- `Frontiers/BalancedUrlFrontier.cs` — IP-based static bucket assignment (IMPLEMENTED)
- `Dns/DnsCache.cs` — DNS resolution cache used for IP bucketing
- `Filters/BlockListFilter.cs` — deny-rule matching from `block-list.txt`
- `Logging/RemainingUrlLogger.cs` — logs unfetched URLs on shutdown (with AutoFlush + directory creation)

### IP Bucketing (implemented)
`queueForUrl` resolves the hostname to an IP via `DnsCache.Global.GetLookup()`, hashes the IP string, and mods by thread count. Falls back to hostname if DNS fails. This prevents multiple Flounder/shared-host capsules from landing in different politeness buckets.

---

## Indexer (`Indexer/`)

The indexer supports **only the original CLI**: `-s`/`--search`, `-a`/`--archive`, `-o`/`--output`, and `-h`/`--help`, plus positional WARC files. This is an explicit user requirement; do not add new flags. Select at least one destination and an existing output directory. Search writes `kennedy2.db`; archive writes `archive.db`, `Packs/`, and stats into that directory. The language profile is packaged with the indexer.

DatabaseMaintenance creates missing Kennedy2 tables, columns, and indexes, consolidates duplicate document rows, and repairs derived structures. It rejects legacy main databases. Completion records are separate per destination and keyed by immutable file path/size/mtime. Interrupted or malformed files are not marked complete. Missing full-text entries cause automatic replay of supplied search inputs.

Imports skip unchanged search payloads before body parsing, while checking for missing document/image/FTS and special-file entries. File completion is independent of durable post-processing queues: a WARC interrupted during finalization is skipped on rerun, and its queued work still drains without response replay. All selected schema repair and post-processing run automatically; there is no separate maintenance mode. Publication uses the existing workflow after writers stop. See OPERATIONS.md.

The WARC response reader buffers decompressed headers and skips allocating unrelated metadata bodies. A bounded channel overlaps reading and the two independent database writers; classification is unchanged. Search batches preload images, reuse FTS statements, and enqueue dirty URLs with prepared SQL upserts instead of tracking a work-item entity per URL. ArchiveBatchWriter probes capture keys before serialization, uses prepared SQL in one transaction per batch, and reuses bounded pack handles. Bytes are flushed before their offsets commit. Capsule and archive hash/URL/date indexes are installed automatically. Empty robots policies use set-based visibility updates.

ArchiveImporter persists captures and visibility work, applies the latest archiver robots policy, and refreshes archive-stats.json including actual pack size. Archived responses are cloned before size limiting so search receives the original payload. Older historical captures remain accessible. Cached/raw/reader/history/diff routes retain the existing archive layout.

---

## FTS Maintenance

```sql
-- Compact FTS segments after bulk deletes (non-blocking in WAL mode)
INSERT INTO DocumentsFts(DocumentsFts) VALUES('optimize');
INSERT INTO UrlSearch(UrlSearch) VALUES('optimize');

-- Full rebuild (heavier, but fixes corruption)
INSERT INTO DocumentsFts(DocumentsFts) VALUES('rebuild');
```

Run after large indexing sessions. Does not block reads in WAL mode. VACUUM (reclaims disk space) does block everything — use scheduled maintenance window or `VACUUM INTO` for a non-blocking copy.

---

## Planned Improvements (not yet implemented)

See `crawler-improvements.md` for full details with design notes.

| # | Item | Status |
|---|---|---|
| 1 | Mercator two-level heap frontier (decouple politeness from worker assignment) | Not started |
| 2 | IP-based politeness bucketing | **Implemented** |
| 3 | Intra-queue URL ordering (authority_count + content_type_rank + inlink_score) | Not started |
| 4 | Per-IP contact timing with variable delay for shared-host IPs | Not started |
| 5 | UrlRegistry as crawl frontier (seed from DB instead of seed file) | Not started |
| 6 | `NextCrawlAt` field on UrlRecord + recrawl scheduling | Not started |
| 7 | `PriorityScore` driven by inlink count from UrlLinks | Not started |
| 8 | In-memory LRU cache (~50K entries) for recently-seen URLs | Not started |
| 9 | Content deduplication via normalized body hash | Not started |
| 10 | Crawler trap detection (per-host URL cap, depth penalty) | Not started |
| 11 | DNS cache TTL expiry and re-resolution | Not started |

Suggested next session starting points:
- **Item 6 (`NextCrawlAt`)**: Add field to `UrlRecord`, wire into `ApplyUrlLifecycle` with the scheduling logic from `crawler-improvements.md`. Straightforward EF model change + small logic block.
- **Item 5 (UrlRegistry frontier)**: Add startup query to `WebCrawler` that seeds `UrlFrontier` from `UrlRegistry WHERE NextCrawlAt <= now ORDER BY PriorityScore DESC`. Requires Item 6 first.
- **Item 1 (heap frontier)**: Larger refactor of `BalancedUrlFrontier`. Do after Items 5 and 6 so the frontier has real scheduling data to work with.

---

## Things to Know / Gotchas

- **Server smoke checks:** `dotnet run --project Kennedy.SmokeTests -- --settings <server.json>` starts the real server on an available high port and checks the home page, text/image searches for `cat`, public tools, reports, stats, and capsule lists using Gemini.Net. `--base-url gemini://localhost:1966/` checks an existing server. This runner uses existing data and does not import, index, or upgrade databases. See README.md for options, network requirements, saved responses, and exit codes. It builds with the solution and runs only when invoked explicitly.

- **Kennedy-1 is read-only reference.** Never modify `/Users/billy/Code/Kennedy-1`. It is the original codebase to port from, not to edit.
- **Schema maintenance is automatic for Kennedy2.** Use DatabaseMaintenance through the indexer; model additions do not require deleting the database or manual SQL. Raw response bodies still require their source WARCs for text reconstruction.
- **The `UrlFrontierEntry` type** lives in `Kennedy.Crawler` namespace (not `Kennedy.Data`). It was moved during the Kennedy-1 → Kennedy-2 port.
- **WAL mode is on.** `ApplyPerformancePragmasAsync` sets journal_mode=WAL, synchronous=NORMAL, cache_size=-65536 (64 MB), mmap_size=256 MB. Called at Indexer startup. Does NOT get called automatically during live crawling (Crawler doesn't use EF Core DbContext directly).
- **FTS `UrlSearch` was returning dead URLs** before the fix at `SqliteSearchService.cs` line 327. The fix adds `AND u.Status = @active_status` to the inurl: filter.
- **`DocumentImageRecord.LastIndexedUtc`** was added in the last session. Existing DB rows will have `DateTime.MinValue` (0001-01-01) for this field until they are re-indexed, which is correct — any real WARC date will be newer, so the guard will allow updating them.
