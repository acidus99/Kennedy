# Kennedy 2.0 — Gemini Search Engine Project Brief for Codex

## Purpose

Kennedy 2.0 is a Gemini search engine and archive/indexing system.

The goal is to crawl Gemini capsules, preserve responses in WARC-style archival storage, and build a searchable SQLite/FTS5 index over a large corpus of Gemini content.

This brief is specifically about the Gemini search engine design. It intentionally excludes unrelated Gemini browser work such as MajorTom.

---

## Core Design Goals

Kennedy 2.0 should:

- Crawl a large number of Gemini URLs.
- Store crawl results in WARC or WARC-like archives.
- Ingest large sets of WARCs into a persistent database.
- Keep URL lifecycle/status tracking separate from indexed document content.
- Build and maintain a SQLite FTS5 search index.
- Track links between Gemini pages.
- Support incremental updates instead of rebuilding everything from scratch.
- Avoid getting trapped in large low-value repetitive URL spaces.
- Preserve historical response data separately from the active searchable index.

---

## Important Architectural Principle

Do not treat “URL” and “document” as the same thing.

A URL is a long-lived crawl target with lifecycle state.

A document is indexable content observed at a URL at a point in time, usually representing the current or latest successfully fetched searchable content.

This is why Kennedy 2.0 should have a separate `UrlRegistry` table from a `Documents` table.

---

## Main Database Concepts

## UrlRegistry

`UrlRegistry` is the authoritative registry of known URLs.

It should track crawl lifecycle and scheduling state, not full indexed content.

Conceptual fields:

- `Id`
- `Url`
- `NormalizedUrl`
- `Scheme`
- `Host`
- `Path`
- `FirstSeenUtc`
- `LastSeenUtc`
- `LastAttemptUtc`
- `LastSuccessUtc`
- `LastStatusCode`
- `LastMeta`
- `IsPublic`
- `IsDenied`
- `DeniedReason`
- `RobotsExcluded`
- `FailureCount`
- `SuccessCount`
- `NextFetchUtc`
- `Priority`
- `AvailabilityScore`
- `VolatilityScore`

Responsibilities:

- Deduplicate normalized URLs.
- Track whether a URL should be crawled again.
- Track permanent failures, redirects, robots exclusions, denylist state, and temporary errors.
- Support crawl scheduling decisions.
- Exist even when the URL currently has no indexable document.

---

## Documents

`Documents` represents searchable/indexable content.

This should be separate from `UrlRegistry`.

Conceptual fields:

- `Id`
- `UrlRegistryId`
- `CanonicalUrl`
- `Title`
- `Content`
- `ContentType`
- `GeminiStatusCode`
- `Meta`
- `DetectedLanguage`
- `LineCount`
- `WordCount`
- `ContentHash`
- `ArchiveRecordId`
- `FirstIndexedUtc`
- `LastIndexedUtc`
- `LastModifiedUtc`
- `IsCurrent`
- `IsSearchable`

Responsibilities:

- Store the current searchable representation of a successfully fetched Gemini response.
- Feed the FTS5 index.
- Point back to the URL registry entry.
- Point into archival storage when needed.
- Allow re-indexing without losing URL lifecycle history.

A URL may exist in `UrlRegistry` without a corresponding searchable row in `Documents`.

Examples:

- Timeout
- TLS error
- DNS error
- Robots excluded
- Non-text response
- Redirect-only URL
- Gone/deleted content
- Denylisted URL

---

## FTS5 Index

Use SQLite FTS5 for search.

Likely indexed fields:

- `Title`
- `Content`
- Possibly URL text or host/path tokens

The FTS table can be maintained either by:

- explicit application code during indexing, or
- SQLite triggers

For simplicity and debuggability, explicit application-level updates may be preferable unless triggers clearly simplify the implementation.

Search should return document rows, then join back to URL metadata where needed.

---

## Links Table

Track outgoing links discovered in Gemini documents.

Conceptual fields:

- `Id`
- `SourceUrlRegistryId`
- `TargetUrlRegistryId`
- `SourceDocumentId`
- `TargetUrl`
- `NormalizedTargetUrl`
- `LinkText`
- `FirstSeenUtc`
- `LastSeenUtc`

Responsibilities:

- Preserve source → target relationships.
- Allow in-link based ranking.
- Help discover new URLs.
- Help prioritize future crawls.
- Support link graph analysis.

A target URL may be known before it has ever been successfully crawled.

---

## Archive / WARC Ingestion

Kennedy 2.0 should support ingesting a large number of WARCs.

The WARC/archive layer is distinct from the active search index.

Archive responsibilities:

- Preserve raw Gemini responses.
- Preserve response metadata.
- Preserve fetch timestamps.
- Preserve status/meta lines.
- Deduplicate repeated content when practical.
- Allow rebuilding the search index later.

Search index responsibilities:

- Store normalized, searchable representation.
- Store current/latest indexable content.
- Track document metadata needed for search and ranking.

Do not require the active search index to contain every historical copy of every response.

Historical copies belong in archive tables/storage.

---

## WARC Ingestion Pipeline

A reasonable ingestion flow:

1. Read WARC records.
2. Extract Gemini URL, fetch timestamp, status, meta, and response body.
3. Normalize URL.
4. Upsert URL into `UrlRegistry`.
5. Record fetch/crawl status.
6. Store or reference archive record.
7. Decide whether the response is indexable.
8. If indexable, parse Gemtext.
9. Extract title, text, links, and metadata.
10. Upsert current row in `Documents`.
11. Update FTS5 index.
12. Upsert discovered outgoing links.
13. Add newly discovered target URLs to `UrlRegistry`.

---

## URL Normalization

URL normalization should be centralized and deterministic.

Important normalization concerns:

- Lowercase scheme and host.
- Normalize default Gemini port.
- Normalize path where safe.
- Preserve meaningful path casing if necessary.
- Remove fragments.
- Carefully handle trailing slashes.
- Carefully handle percent-encoding.
- Normalize relative links against source URL.

Avoid scattering URL normalization logic across crawler, indexer, and search code.

---

## Indexing Strategy

Kennedy 2.0 should support incremental indexing.

Avoid assuming the database is rebuilt from scratch every time.

Preferred behavior:

- Ingest new WARC records.
- Update URL lifecycle data.
- Update documents only when content changes.
- Avoid unnecessary FTS churn when content hash is unchanged.
- Batch writes for performance.
- Keep batching hidden inside storage/indexing services rather than forcing every caller to manage it.

---

## EF Core / SQLite Concerns

The project uses or may use EF Core with SQLite.

Important concerns from prior design discussions:

- Avoid excessive `SaveChanges` calls.
- Avoid repeated `SingleOrDefaultAsync` lookups during large imports.
- Avoid write amplification.
- Batch inserts/updates.
- Use unique constraints on normalized URLs.
- Encapsulate batching in repository/store classes.
- Keep caller code simple.

A useful direction is a `UrlRegistryStore` or similar service that can:

- cache known URL IDs during an import batch,
- stage new URL records,
- flush in batches,
- resolve normalized URL → ID,
- hide EF Core batching details from callers.

---

## Crawler Trap / Low-Value Rat Nest Detection

Kennedy 2.0 needs to avoid indexing huge low-value repetitive areas.

Examples:

- Git commit trees
- endless package versions
- generated archives
- near-infinite calendar/version paths
- repetitive templated pages

But it should still allow valuable large collections such as:

- RFC indexes
- curated document collections
- meaningful archives
- large but human-useful Gemini directories

Preferred approach:

- heuristic/statistical,
- not just hardcoded path rules.

Signals worth considering:

- many URLs with highly similar paths,
- many documents with near-identical titles,
- many documents with near-identical body structure,
- low unique-content ratio,
- many numeric/hash/version-looking path segments,
- extreme URL fanout from one page or directory,
- low inbound-link diversity,
- shallow template variation,
- repeated boilerplate with tiny differences.

Crawler trap handling should be conservative.

It is better to deprioritize suspicious regions first than to permanently delete or block them too aggressively.

Potential actions:

- reduce crawl priority,
- cap crawl depth per host/path cluster,
- mark path cluster as suspicious,
- require stronger in-link evidence before continuing,
- exclude from search only when confidence is high.

---

## Crawl Scheduling Concepts

The `UrlRegistry` should eventually support smarter recrawl scheduling.

Possible signals:

- Last successful fetch time
- Last attempted fetch time
- Failure count
- Availability score
- Volatility score
- In-link count
- Host-level quality
- Historical content change rate
- Robots/denylist state
- Priority boosts for important known pages

Possible scheduling model:

- stable content gets crawled less often,
- frequently changing content gets crawled more often,
- repeatedly failing URLs back off,
- high in-link URLs get higher priority,
- suspicious repetitive URL clusters get lower priority.

---

## Search Ranking Concepts

Initial ranking can rely mostly on FTS5.

Later ranking can combine:

- FTS5 score
- title match boost
- URL/host match boost
- in-link count
- freshness
- host quality
- document length normalization
- duplicate/near-duplicate suppression

Keep the first implementation simple.

---

## Separation of Concerns

Recommended logical components:

## Crawler

Fetches Gemini URLs and writes raw results/archive records.

## Archive Reader / WARC Reader

Reads historical crawl records from WARC files.

## Indexer

Turns crawl/archive records into searchable documents.

## UrlRegistryStore

Manages normalized URL identity, lifecycle state, and scheduling metadata.

## DocumentStore

Manages current searchable documents.

## LinkStore

Manages extracted links and URL discovery.

## SearchService

Runs FTS queries and returns ranked results.

---

## Coding Style Preferences

When Codex works on this project, optimize for:

- simple code,
- explicit behavior,
- minimal dependencies,
- predictable data flow,
- SQLite-friendly batching,
- clear error handling,
- understandable logs,
- testable services,
- command-line friendliness.

Avoid:

- over-abstracted repository layers,
- magical framework behavior,
- unnecessary background services,
- dependency sprawl,
- premature distributed-system architecture,
- complex ranking before basic indexing is correct.

---

## Good Initial Codex Tasks

Useful tasks for Codex:

- Design or refine the SQLite schema.
- Implement `UrlRegistry` and `Documents` as separate entities.
- Add unique constraint/index for normalized URLs.
- Implement WARC ingestion into `UrlRegistry`.
- Implement WARC ingestion into `Documents`.
- Implement FTS5 table creation and update logic.
- Add batch upsert logic for URL registry entries.
- Add link extraction and `Links` table upserts.
- Add content hashing to skip unchanged documents.
- Add crawl-trap scoring heuristics.
- Add CLI commands for importing WARCs.
- Add CLI commands for rebuilding the FTS index.
- Add tests for URL normalization.
- Add tests for WARC-to-document ingestion.
- Add tests for duplicate URL handling.

---

## Non-Goals for This Brief

This brief does not cover:

- MajorTom Gemini browser design
- MAUI UI work
- Stargate HTTP-to-Gemini proxy work
- Sales/product/company context
- General Gemini client UX

This is only about Kennedy 2.0 as a Gemini search engine, archive, crawler, WARC ingestion, URL registry, document index, and SQLite/FTS5 search system.
