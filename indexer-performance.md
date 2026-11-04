# Indexer performance measurements — 2026-11-04

Measurements used the real `2026-08-22.warc.gz` from the local WARC collection. No production database or pack was changed. Temporary inputs, harnesses, raw JSON results, and logs are retained at `/private/tmp/kennedy2-record-benchmark-2026-11-04`.

## Sample and comparisons

The source contains 1,424,232 WARC records, including 710,286 Gemini responses. Seed 20261104 selected one random response from each consecutive block of four, plus one from the final partial block: **177,572 responses (25.00007%)**, spread through the entire crawl. The response-only sample is 7,598,276,140 bytes uncompressed.

The state-loading comparison changes only how the current writer obtains tracked URL, document, and image entities. The per-URL variant executes a left-joined query per distinct source URL. The batch variant uses the current three bulk queries. Parsing, response-hash rules, writes, FTS updates, and post-processing are otherwise identical. This isolates the loading strategy on the current schema; it is not a comparison of complete old and new indexer binaries.

Three workloads distinguish separate questions:

- State loading alone: current populated database, without parsing or writes. Both strategies must return the same entities and checksum.
- Incremental ingestion: identical copies of the search database backed up before this crawl. Includes actual inserts, updates, parsing, FTS changes, and both durable search post-processing queues.
- Fresh ingestion: empty search databases, importing the same sample with both strategies. Exercises content ingestion without a previously imported crawl suppressing the work.

Timed ingestion excludes schema initialization and validation. Reading the response-only sample, Gemini response decoding, and response hashing are included. These search comparisons do not include archive writing or the production pipeline's overlapping reader/archive tasks. End-to-end pairs are single local runs; filesystem caching, JIT startup, and machine load limit the precision of small differences. Incremental runs use per-URL then batch order; fresh runs use the reverse order.

## State loading alone

After a full-sample warmup, run order was per-URL, batch, batch, per-URL. All runs loaded 177,572 URL entities, 128,646 documents, and 15,538 images, with identical checksums.

| Strategy | Queries | Mean state-loading time |
|---|---:|---:|
| Per-URL join | 177,572 | 18.237 s |
| Three queries per batch | 1,068 | 2.290 s |

Batch loading was **7.96× faster for this phase**, saving 15.95 seconds on the quarter sample. This does not imply an eightfold improvement in complete ingestion.

## Complete incremental search ingestion

Both runs began with independent copies of the same pre-crawl search backup. Each read 177,572 responses, parsed 58,620 (33.0%), and skipped 116,016 unchanged responses (65.3%); the remainder followed transient/older-record paths. Thus this comparison includes substantial actual content ingestion rather than only replay skips.

| Phase | Per-URL loading | Batch loading |
|---|---:|---:|
| State loading, including shared special-file/FTS checks | 41.432 s | 32.243 s |
| Body parsing/classification | 37.374 s | 38.991 s |
| Entire ingestion, including reading and writing | 148.407 s | 145.907 s |
| Robots and file-search post-processing | 5.797 s | 5.732 s |
| Total | **154.205 s** | **151.640 s** |

The observed total saving was **2.565 seconds (1.7%)**, a small difference in a single pair of runs. Shared work and runtime variation dominate much of the ingestion; the isolated 7.96× loading result does not translate into a large end-to-end gain. State-phase instrumentation here includes shared special-file and missing-FTS queries, whereas the earlier isolated benchmark measures only the URL/document/image queries.

Digests of every scalar row in the registry, documents, images, links, special-file tables, work queue, and all three public FTS tables matched exactly across strategies. Both runs changed documents from 545,063 to 559,465, URL registry entries from 1,314,505 to 1,355,140, and images from 28,450 to 37,623. Both completed 867 capsule robots jobs and processed 144,203 dirty file-search URLs, refreshing 25,928.

## Fresh search ingestion

The fresh batch-loading run parsed 174,636 responses and skipped zero unchanged responses. It created 128,459 documents, 477,601 registry URLs, and 15,536 images. It completed 892 robots-policy jobs and processed 473,308 dirty URLs, refreshing 36,818 file-search entries. The remaining 2,936 responses followed transient-only paths.

| Phase | Per-URL loading | Batch loading |
|---|---:|---:|
| State loading, including shared special-file/FTS checks | 23.906 s | 1.281 s |
| Body parsing/classification | 313.516 s | 347.289 s |
| Entire ingestion, including reading and writing | 513.896 s | 536.399 s |
| Robots and file-search post-processing | 7.920 s | 16.222 s |
| Total | **521.816 s** | **552.620 s** |

Batch loading saved 22.626 seconds in the state phase, but its complete run took 30.805 seconds longer (5.9%). Both outputs matched exactly, including every scalar row and all public FTS rows. The unchanged parser and post-processing phases varied substantially between runs, exceeding the state-loading saving. This pair therefore **does not demonstrate an end-to-end speedup**. A precise whole-import claim would need repeated, balanced runs controlling warmup and machine conditions. Even holding all other work fixed, this phase saving represents only about 4% of the observed fresh-run total. Classification is unchanged in all experiments.

## Archive index audit

All hash-only consumers were checked, including pack-offset reuse and global duplicate detection in `Archiver` and `ArchiveBatchWriter`. `(DataHash,UrlId)` serves these queries through its leading `DataHash` column. No remaining caller requires a separate hash-only index. Same-URL earliest-capture queries locate the hash/URL matches and sort that small result by capture date.

A full metadata-copy measurement allocated 1,336,414,208 bytes to the two-column index, versus 1,647,853,568 bytes for the three-column index and 1,356,746,752 bytes for the original hash-only index. Replacing both existing hash indexes with the pair saves about **1.55 GiB of allocated index pages**, including about 297 MiB from omitting `Captured`. Pages become reusable; the physical database file does not shrink automatically. Index-build density and future writes can change these sizes.

On 100 sampled hash/URL groups, prepared warm earliest-capture lookups averaged 13.76 µs with two columns versus 4.02 µs with three. Matching groups contained 1–48 captures (mean 18.82). Thus the two-column choice accepts extra table reads and a small sort to reduce storage and index maintenance. These warm point-query timings do not establish a complete-import speedup.

The capsule composite `(Protocol,Domain,Port)` is retained: archive robots processing uses the full capsule key and pages URLs by ID. SQLite's row-ID suffix supports this ordering without an explicit fourth column. No current archive query needs a protocol-only, domain-only, or port-only lookup, so those three indexes are removed. Archive statistics still return the same 4,927 distinct domain/port pairs in the metadata copy; its plan changes from a domain-index scan with table reads to a covering composite-index scan. Cold-before/warm-after timings are not treated as a valid speed comparison.

Index replacement and legacy input-ledger removal are transactional. The importer logs index creation and uses file-backed temporary sorting. Production receives the upgrade on its next normal indexer run.

## Pack streams

Cryptographic hash prefixes distribute new payload writes over 65,536 packs, and existing identical payloads reuse offsets without appending. The completed quarter-sample trace found only 123 cache hits among 148,672 unique fresh payload writes (0.083%), and one among 112 payloads absent from the pre-crawl archive. These are sample trace counts, not measured throughput gains.

The 64-handle cache is removed; each pack is closed after appending. Pack routing, record format, returned byte offsets, compression, and per-batch content-hash deduplication stay the same. Closing flushes the append before its metadata transaction commits; this does not add a cross-file power-loss guarantee.

## Validation and artifacts

The Release solution build passed. Disposable-data checks passed for legacy index/ledger migration, capture preservation, repeated capture idempotence, earlier-capture duplicate classification, META-only response changes, and reading both existing and newly written pack offsets.

The normal indexer CLI then imported six fixture responses into search and archive. The fixture included a valid image, two different payloads deliberately sharing a four-hex pack prefix, and an identical payload at a second URL. Reading records back at the stored offsets reproduced the exact response bytes and hashes; identical payloads reused the same offset. After deleting a document's FTS entry and creating legacy input-ledger tables, a renamed/reordered WARC repaired the FTS entry, removed both ledgers, and added zero archive captures. Registry/capture counts and all pack bytes remained unchanged. Both work queues were empty, and SQLite quick checks returned `ok`.

Raw results in the temporary benchmark directory:

- `sample.json`, `quarter.warc`, `quarter-urls.txt`: source counts and deterministic sample.
- `state-results.json`, `state/`: warmed loading-only ABBA results and harness.
- `ingest-results.json`, `ingest-output-digests.json`, `ingest/`: pre-crawl ingestion results, full logical-output comparison, and harness.
- `fresh-results.json`, `fresh-output-digests.json`, `fresh/`: empty-database ingestion results, output comparison, and harness.
- `pack-trace-summary.json`: completed prefix-cache trace counts; no cache I/O timing experiment was needed.
- `capsule-index-audit.json`: statistics count and query-plan audit.
- `final-build.log`, `final-checks.log`, `cli-replay-results.json`, `cli-replay.py`: final verification evidence.

The timed writer variants derive from `ResponseStore` at `1fabd96`; only URL/document/image loading differs, with phase timers added. The incremental seed is the search backup in `/private/tmp/kennedy2-import-2026-11-03-442838fa/backups/`, taken before the crawl import. To repeat ingestion measurements, restore independent copies of that seed or create new empty databases before each run; reusing the finished benchmark databases would measure replay instead. No whole production import was started, and the production schema has not been modified by these experiments.
