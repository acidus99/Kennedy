# Indexer Performance Improvements

Changes made to `Kennedy.Data` and `Indexer` to reduce WARC ingestion time.

---

## #1 — Batch transactions (one transaction per 500 records, not one per record)

**Problem:** `ResponseStore.StoreResponseAsync` opened a new SQLite transaction and committed it for every single WARC record. SQLite's default journal mode requires an fsync on each commit. At 500,000 records, that is 500,000 fsyncs — the dominant cost at scale.

**Fix:** Added `ResponseStore.StoreBatchAsync(IReadOnlyList<GeminiResponse>, ct)`. The `WarcIndexer` now accumulates responses into a `List<GeminiResponse>` of up to 500 entries and calls `StoreBatchAsync` at each flush boundary instead of calling `StoreResponseAsync` per record.

Inside `StoreBatchAsync`, all N responses are processed under **one transaction** with three `SaveChanges` calls (regardless of N):

1. **Phase 1** — All source `UrlRecord` upserts: one `IN` query loads pre-existing rows; the rest are fresh `INSERT`s. Single `SaveChanges` to flush and populate auto-generated IDs.
2. **Phase 2** — All `DocumentRecord` upserts + `UrlRecord` inserts for newly discovered link-target URLs. Single `SaveChanges`.
3. **Phase 3** — Stale `UrlLink` delete (raw SQL, one statement), then fresh `UrlLink` inserts. Single `SaveChanges`.
4. Single `COMMIT`.

**FK safety:** Phase 1 always runs before Phase 2 (Documents reference UrlRegistry IDs). Phase 2 always runs before Phase 3 (UrlLinks reference both UrlRegistry IDs). No FK constraint can be violated.

**Files changed:**
- `Kennedy.Data/Services/ResponseStore.cs` — added `StoreBatchAsync`, extracted `ApplyDocumentToContext` and `ApplyUrlMetadata` helpers
- `Indexer/WarcIndexer.cs` — accumulates into `batch`, flushes every `BatchSize = 500` records

**Expected impact:** ~100× reduction in commit count → dominant speedup for large WARCs.

---

## #2 — SQLite performance pragmas

**Problem:** The SQLite connection used default settings: rollback-journal mode with `synchronous=FULL`, a small page cache, and no memory mapping. These are safe defaults for a general-purpose application but are conservative for bulk ingestion.

**Fix:** Added `KennedyDbContext.ApplyPerformancePragmasAsync()`, called once from `Program.cs` after `EnsureCreated`:

| Pragma | Value | Reason |
|---|---|---|
| `journal_mode` | `WAL` | Write-Ahead Logging allows readers to proceed during writes; reduces fsync pressure at checkpoint time rather than per-commit |
| `synchronous` | `NORMAL` | Skips per-commit fsync; safe under WAL (data is durable at the next checkpoint) |
| `cache_size` | `-65536` | 64 MB page cache; reduces re-reads during the IN-query lookups in Phase 1/2 |
| `temp_store` | `MEMORY` | Keeps SQLite's internal temporary tables in RAM rather than on disk |
| `mmap_size` | `268435456` | 256 MB memory-mapped I/O window; speeds up sequential read passes |

**Files changed:**
- `Kennedy.Data/KennedyDbContext.cs` — added `ApplyPerformancePragmasAsync`
- `Indexer/Program.cs` — calls `ApplyPerformancePragmasAsync` inside `EnsureDatabaseCreatedAsync`

**Expected impact:** 3–5× write throughput improvement on top of the batching gains, particularly on spinning disk or network-attached storage.

---

## #4 — Raw SQL DELETE for UrlLinks (instead of EF RemoveRange)

**Problem:** The previous `UpdateLinksAsync` code did:
```csharp
var previousLinks = db.UrlLinks.Where(x => x.SourceUrlId == sourceUrl.Id);
db.UrlLinks.RemoveRange(previousLinks);
```
EF Core evaluates the `Where()` lazily inside `RemoveRange` by loading all matching rows into memory, then tracking and issuing an individual `DELETE` statement for each one. A page with 50 outbound links produces 50 round-trips.

**Fix:** Replaced with a single parameterless raw SQL statement in both the single-record path (`UpdateLinksAsync`) and the batch path (`StoreBatchAsync`):

Single-record:
```csharp
await db.Database.ExecuteSqlRawAsync(
    $"DELETE FROM UrlLinks WHERE SourceUrlId = {sourceUrl.Id}", ct);
```

Batch (all source IDs in one statement):
```csharp
var sourceIdsCsv = string.Join(",", urlMap.Values.Select(u => u.Id));
await db.Database.ExecuteSqlRawAsync(
    $"DELETE FROM UrlLinks WHERE SourceUrlId IN ({sourceIdsCsv})", ct);
```

The IDs are `long` values from the database, not user input, so direct interpolation is safe.

**Files changed:**
- `Kennedy.Data/Services/ResponseStore.cs` — both `UpdateLinksAsync` (single-record) and `StoreBatchAsync` (batch)

**Expected impact:** Eliminates N individual DELETE round-trips per page, replacing them with 1 statement. Particularly meaningful for pages with many outbound links.

---

## #6 — One DbContext per batch (not one per record)

**Problem:** `ResponseStore.StoreResponseAsync` called `_dbFactory.CreateDbContextAsync()` for every WARC record, creating and disposing a `KennedyDbContext` on each call. While cheap individually, this adds allocation and EF setup overhead O(N) times.

**Fix:** `StoreBatchAsync` calls `_dbFactory.CreateDbContextAsync()` once per batch, sharing the context across all N records in that batch. The context is disposed at the end of the batch along with the transaction.

`StoreResponseAsync` (single-record path) is unchanged and still creates its own context; it remains available for one-off use outside of bulk ingestion.

**Files changed:**
- `Kennedy.Data/Services/ResponseStore.cs` — `StoreBatchAsync` creates context once per batch call
- `Indexer/WarcIndexer.cs` — calls `StoreBatchAsync` instead of per-record `StoreResponseAsync`

**Expected impact:** Minor in isolation; meaningful in combination with batching because the shared context's change tracker does not need to be re-initialized between records.
