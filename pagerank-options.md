# Kennedy Search Ranking: Old System Analysis & Proposed PageRank Implementation

## Context

Kennedy is a Gemini-protocol search engine. This document covers:
1. How the old Kennedy (Kennedy-1) ranked search results
2. Why the new Kennedy (this codebase, `kennedy2` branch) currently ranks poorly
3. A concrete proposed PageRank implementation for new Kennedy

---

## Old Kennedy Ranking System

### Where to find it
- `PopularityCalculator`: `/Volumes/billy/Code/Kennedy-1/SearchIndex/Web/PopularityCalculator.cs`
- Ranking formula in search query: `/Volumes/billy/Code/Kennedy-1/SearchIndex/Search/SearchDatabase.cs` line 299

### How it worked

**Step 1 — Compute a popularity score per page (run as a batch job):**

```csharp
entry.PopularityRank = 1;  // baseline
foreach (var sourceID in externalLinksPointingToThisPage)
{
    entry.ExternalInboundLinks++;
    entry.PopularityRank += 1;           // each external inbound link = +1 vote
}
entry.PopularityRank = Math.Min(entry.PopularityRank, 100);   // clip at 100
entry.PopularityRank = Math.Log(entry.PopularityRank, 100);   // log base 100 → range [0.0, 1.0]
```

Key points:
- "External" means `Links.IsExternal = true` — a link from a different capsule (hostname)
- Every linking page has equal weight regardless of how popular *it* is (not true PageRank)
- The log base-100 compression: 1 inbound link → ~0.0, 10 links → ~0.5, 99 links → ~1.0
- Stored as `Documents.PopularityRank` (double) and `Documents.ExternalInboundLinks` (int)
- Note: this is **per-page**, not per-capsule, despite the original intent. A link to
  `gemini://capsule.example/index.gmi` only boosts that specific page, not all pages on the capsule.

**Step 2 — Apply at query time:**

```sql
ORDER BY (rank + (rank * 0.3 * PopularityRank)) * IIF(ContentType = 1, 1.03, 1)
```

Where:
- `rank` is SQLite FTS5's built-in BM25 score — **a negative number** (more negative = better match)
- `PopularityRank` is the pre-computed score in [0.0, 1.0]
- The formula boosts popular pages by up to **30%** (when PopularityRank = 1.0)
- Gemtext (ContentType = 1) gets an additional **1.03× nudge** over plain text
- `ORDER BY` ascending — most negative value ranks first

### What was wrong with this approach

1. **Not true PageRank** — all linkers have equal weight. A link from a major Gemini aggregator 
   counts the same as a link from a new capsule nobody has heard of.
2. **Per-page not per-capsule** — Gemlog posts are almost never directly linked to externally.
   Only the capsule's index page accumulates votes. Individual posts on popular capsules get 
   zero popularity boost even if the capsule is well-known.

### Actual data from old Kennedy's database

Measured against `/tmp/doc-index.db` (old Kennedy production DB):

| Metric | Value |
|---|---|
| Total documents | 506,168 |
| Total links | 1,885,269 |
| External links (IsExternal=1) | 107,114 |
| Internal links | 1,778,155 |
| Unique docs with any external inbound links | 36,531 (7.2%) |
| External links where BOTH src and target are Documents | 82,267 |
| Unique docs with inbound links (Documents-only filter) | 19,459 |
| Average external outbound links per source | 3.38 |
| Max external outbound links from one source | 8,292 |

**Key finding: 92.8% of pages have zero external inbound links.** This means pure per-page 
link counting gives almost no signal for the vast majority of search results. This is why a 
capsule-level component is essential for Geminispace.

---

## Current New Kennedy Ranking

New Kennedy currently sorts by:
```sql
ORDER BY bm25(DocumentsFts), d.LastIndexedUtc DESC
```

Pure BM25 relevance, falling back to recency. No popularity signal at all. This is a regression
from old Kennedy.

### New Kennedy search query structure (after recent refactoring)

The search query joins only `Documents` + `DocumentsFts` — **no UrlRegistry join** for the 
main search path. `Host` and `LastMimeType` were denormalized into `DocumentRecord` so site: 
and filetype: filters work without joining UrlRegistry:

```sql
SELECT d.CanonicalUrl, d.Title, d.LastMimeType, d.DetectedLanguage, d.LineCount,
       d.BodySize, d.IsBodyTruncated, snippet(DocumentsFts, 1, '[',']','…',20) AS Snippet
FROM Documents d
INNER JOIN DocumentsFts ON DocumentsFts.rowid = d.Id
WHERE DocumentsFts MATCH @fts_query
ORDER BY bm25(DocumentsFts), d.LastIndexedUtc DESC
LIMIT @limit OFFSET @offset
```

`PopularityRank` should be added to `DocumentRecord` (denormalized from `UrlRecord`) and 
slotted into the ORDER BY formula.

---

## Proposed PageRank Implementation for New Kennedy

### Why true PageRank instead of old Kennedy's approach

True PageRank propagates rank through the graph: a link from a well-linked page is worth more
than a link from an obscure page. In old Kennedy's system, both counted as 1 vote.

### Scope: Documents only, not all of UrlRegistry

**Only compute PageRank over pages that have a `Documents` entry** (active, indexed text pages).

Rationale:
- Only Documents appear in search results — assigning rank to redirects, 404s, images is wasted
- The link graph filtered to Documents-only retains **77% of external links** (82,267 of 107,114)
  and is a cleaner signal
- Redirects (3x status), gone pages (51 status), images, and binary files naturally fall out

The SQL to build the link graph for the algorithm:
```sql
SELECT l.SourceUrlId, l.TargetUrlId
FROM UrlLinks l
INNER JOIN Documents src ON src.UrlRegistryId = l.SourceUrlId
INNER JOIN Documents tgt ON tgt.UrlRegistryId = l.TargetUrlId
WHERE l.IsExternal = 1
```

In new Kennedy, `UrlLinks.SourceUrlId` and `UrlLinks.TargetUrlId` reference `UrlRegistry.Id`,
while `Documents.UrlRegistryId` is the FK into `UrlRegistry`. The join above is correct.

### Two-level scoring

Because 93% of pages have no external inbound links, pure PageRank gives almost no signal for
most results. The fix is to add a **capsule-level component**:

- **Page score**: true iterative PageRank over the Documents link graph
- **Capsule score**: the sum of all page scores for a given hostname, normalized to [0,1]
- **Final score**: `0.7 × page_score + 0.3 × capsule_score`

This ensures that posts on a well-known capsule get some signal even when nobody links to 
that specific post — because the capsule as a whole is well-represented in the link graph.

### The PageRank algorithm

Standard PageRank with damping factor d = 0.85, run until convergence (or max 50 iterations):

```
rank[A] = (1 - d) + d × Σ( rank[B] / outbound_links[B] )
          for all B that have an external link to A
```

**Critical implementation detail — two arrays, never update in place:**

Each iteration must compute ALL new ranks from the PREVIOUS iteration's ranks. If you update
a page's rank and then use that updated value to compute another page's rank in the same 
iteration, results are wrong. Use two arrays and swap them at the end of each iteration.

### Concrete implementation sketch

```csharp
public class PopularityCalculator
{
    private const double Damping = 0.85;
    private const int MaxIterations = 50;
    private const double ConvergenceThreshold = 1e-6;

    public void Compute(KennedyDbContext db)
    {
        // 1. Load Documents, build index mapping UrlRegistryId → array position
        var docIds = db.Documents
            .Select(d => new { d.Id, d.UrlRegistryId, d.Host })
            .ToList();

        // Map UrlRegistryId → index into rank arrays
        var urlIdToIndex = docIds
            .Where(d => d.UrlRegistryId.HasValue)
            .ToDictionary(d => d.UrlRegistryId!.Value, d => /* index */);
        int n = docIds.Count;

        // 2. Load filtered link graph (both src and target must be Documents)
        var links = db.UrlLinks
            .Where(l => l.IsExternal
                && db.Documents.Any(d => d.UrlRegistryId == l.SourceUrlId)
                && db.Documents.Any(d => d.UrlRegistryId == l.TargetUrlId))
            .Select(l => new { l.SourceUrlId, l.TargetUrlId })
            .ToList();
        // NOTE: for large DBs, do this as a raw SQL query with the JOIN above for performance

        // 3. Compute outbound count per source
        var outboundCount = links
            .GroupBy(l => l.SourceUrlId)
            .ToDictionary(g => g.Key, g => g.Count());

        // 4. Iterative PageRank — two-array swap pattern
        var rank    = new double[n];
        var newRank = new double[n];
        Array.Fill(rank, 1.0);

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            Array.Fill(newRank, 1.0 - Damping);  // teleportation baseline

            foreach (var link in links)
            {
                if (!urlIdToIndex.TryGetValue(link.TargetUrlId, out int ti)) continue;
                if (!urlIdToIndex.TryGetValue(link.SourceUrlId, out int si)) continue;
                newRank[ti] += Damping * rank[si] / outboundCount[link.SourceUrlId];
            }

            // Check convergence
            double delta = 0;
            for (int i = 0; i < n; i++) delta += Math.Abs(newRank[i] - rank[i]);
            (rank, newRank) = (newRank, rank);  // swap — no copying
            if (delta < ConvergenceThreshold) break;
        }

        // 5. Capsule-level aggregation
        // Group by hostname, sum page ranks for that host, normalize to [0,1]
        var capsuleRawScore = new Dictionary<string, double>();
        for (int i = 0; i < docIds.Count; i++)
        {
            var host = docIds[i].Host;
            capsuleRawScore[host] = capsuleRawScore.GetValueOrDefault(host) + rank[i];
        }
        double maxCapsule = capsuleRawScore.Values.Max();
        var capsuleScore = capsuleRawScore
            .ToDictionary(kv => kv.Key, kv => kv.Value / maxCapsule);

        // 6. Combine and log-normalize to [0, 1]
        double minRank = rank.Where(r => r > 0).DefaultIfEmpty(1).Min();
        double maxRank = rank.Max();

        for (int i = 0; i < docIds.Count; i++)
        {
            double pageScore = (rank[i] - minRank) / (maxRank - minRank);  // normalize to [0,1]
            double capsule   = capsuleScore.GetValueOrDefault(docIds[i].Host, 0);
            double combined  = 0.7 * pageScore + 0.3 * capsule;

            // Write to UrlRecord (which gets denormalized to DocumentRecord)
            // ... bulk update via raw SQL for performance (avoid EF change tracking overhead)
        }

        // 7. Bulk update — raw SQL is much faster than EF for 350k+ rows
        // Use a temp table or batch parameterized updates
    }
}
```

**Performance note on step 2**: The LINQ `.Any()` subqueries will be catastrophically slow 
for 350k+ documents. Use the raw SQL JOIN query listed above and load as a plain list of 
`(long sourceId, long targetId)` structs.

### Schema changes needed

**`UrlRecord` (`Kennedy.Data/Models/UrlRecord.cs`):**
```csharp
public double PopularityRank { get; set; } = 0.0;
public int ExternalInboundLinks { get; set; } = 0;
```

**`DocumentRecord` (`Kennedy.Data/Models/DocumentRecord.cs`):**
```csharp
// Denormalized from UrlRecord — same pattern as Host and LastMimeType already there
public double PopularityRank { get; set; } = 0.0;
```

`PopularityRank` needs to be written to both `UrlRecord` (authoritative) and `DocumentRecord`
(denormalized copy for zero-join search queries). `ResponseStore.ApplyDocumentToContext` already
copies `Host` and `LastMimeType` from `url` to `existing` — add `PopularityRank` there too.

### Search query ORDER BY change

In `Kennedy.Search/Services/SqliteSearchService.cs`, change the `orderBy` string:

```csharp
// Current (no popularity signal):
var orderBy = query.HasFtsQuery
    ? " ORDER BY bm25(DocumentsFts), d.LastIndexedUtc DESC"
    : " ORDER BY d.LastIndexedUtc DESC";

// Proposed (BM25 + popularity, matching old Kennedy's formula):
var orderBy = query.HasFtsQuery
    ? " ORDER BY (bm25(DocumentsFts) + bm25(DocumentsFts) * 0.3 * COALESCE(d.PopularityRank, 0.0))" +
      " * IIF(d.ContentType = 1, 1.03, 1.0)"
    : " ORDER BY d.LastIndexedUtc DESC";
```

Note: `bm25()` returns negative values in SQLite FTS5. More negative = better match. The 
popularity term `bm25 * 0.3 * PopularityRank` is also negative (negative × positive = negative),
making popular pages rank even more negative = higher in ASC order. This is the same math 
as old Kennedy's `rank + rank*0.3*PopularityRank`.

### When to run PopularityCalculator

Run it as a separate pass after a full crawl batch completes — not inline during ingestion.
On ~350k documents and ~82k links, the in-memory computation is fast (~50 iterations over 
82k links = ~4M operations). The bottleneck is the bulk SQL write-back of 350k rows.

A good place to call it: in `Indexer/Program.cs` after `StoreBatchAsync` finishes processing 
the last WARC batch, or as a scheduled standalone command.

---

## Memory Estimate for New Kennedy

Based on Documents-only link graph and ~348k documents:

| Structure | Count | Memory |
|---|---|---|
| docIds list | 348,151 | ~8 MB |
| urlIdToIndex dictionary | 348,151 | ~12 MB |
| rank[] array | 348,151 | ~2.8 MB |
| newRank[] array | 348,151 | ~2.8 MB |
| links list | ~82,000 (est.) | ~1.3 MB |
| outboundCount dict | ~25,000 (est.) | ~600 KB |
| **Total** | | **~28 MB** |

Comfortable — no special memory management needed.

---

## Files to Edit (Summary)

| File | Change |
|---|---|
| `Kennedy.Data/Models/UrlRecord.cs` | Add `PopularityRank` (double), `ExternalInboundLinks` (int) |
| `Kennedy.Data/Models/DocumentRecord.cs` | Add `PopularityRank` (double, denormalized) |
| `Kennedy.Data/Services/ResponseStore.cs` | Copy `url.PopularityRank` → `existing.PopularityRank` in `ApplyDocumentToContext` |
| `Kennedy.Search/Services/SqliteSearchService.cs` | Update `orderBy` string to BM25 + popularity formula |
| `Kennedy.Data/Services/PopularityCalculator.cs` | **New file** — implements the two-level PageRank algorithm |
| `Indexer/Program.cs` | Call `PopularityCalculator.Compute()` after crawl batch finishes |
