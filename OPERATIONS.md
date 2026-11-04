# Kennedy2 indexing and publication

Work on the `kennedy2` branch. `Kennedy-1` remains the old reference checkout. Builds require the sibling Gemini.Net, Warc.Net, and RocketForce projects, matching the existing repository layout.

## Import completed WARCs locally

```sh
dotnet run --project Indexer -c Release -- -s -a -o /path/to/crawl-data /path/to/new-crawl.warc.gz
```

Only the original options are supported: `-s`/`--search`, `-a`/`--archive`, `-o`/`--output`, and `-h`/`--help`. Search and archive flags combine in either order. Select at least one and supply an existing output directory. WARC files are positional arguments; use a shell glob to supply multiple files.

The output contains `kennedy2.db` for search and `archive.db`, `Packs/`, and `archive-stats.json` for the archive. The language profile is packaged with the indexer. Schema repair and all selected post-processing run automatically after the supplied WARCs, including when those files have already been imported.

The indexer creates missing Kennedy2 tables, columns, and indexes, consolidates duplicate document rows, and repairs derived entries where possible. It does not upgrade the legacy `doc-index.db`. Missing search text requires its original response bodies: supply the corresponding WARCs to reconstruct it. All supplied records are reconsidered on every invocation; unchanged records can repair missing derived data.

The search index retains the latest usable content across temporary failures, with the actual successful capture date. Newer permanent failures, redirects, interactive/authentication responses, and policy exclusions stop that content appearing in search. Robots exclusions retain the payload internally so lifting the restriction can restore it. Archive visibility follows the `archiver` robots rules independently.

Every invocation reads the supplied WARCs. The database does not track input containers; legacy `WarcImports` tables are removed automatically. Search uses URL/timestamp/payload watermarks and checks derived state before skipping an unchanged body. Archive capture identity remains URL plus capture timestamp, and identical payload bytes reuse their stored pack offsets.

If interrupted during import or post-processing, rerun the same command. Previously committed records are safe to replay and durable search/archive work queues still drain. Missing document, image, full-text, or special-file entries can be reconstructed from the supplied records. Ctrl+C preserves committed batches and pending work. Writer lock files prevent overlapping indexers and may remain after a run; the open file handle is the lock.

The importer overlaps a buffered WARC reader with one writer for each database. At most two batches are queued, with batches capped at 500 responses or about 32 MiB of response bodies (a single oversized response can exceed that bound). Metadata records are drained without allocating their bodies. Progress reports WARC bytes read, skipped payloads/captures, and separate search/archive timings; those timings overlap. Language detection is unchanged.

Archive maintenance replaces the overlapping hash-only and hash/URL/date indexes with one `(DataHash,UrlId)` index. Hash-only queries use its leading column; earliest-capture queries sort the matching captures. Capsule robots-visibility queries use `(Protocol,Domain,Port)` with SQLite’s implicit row-ID suffix. Separate protocol/domain/port indexes are removed because current archive queries do not need them; the statistics projection is covered by the composite. Index upgrades are transactional, report creation progress, and use file-backed temporary storage. Dropped index pages become reusable space; this does not automatically compact the database file. Archive metadata is committed per batch with its visibility work, and each pack append is closed and flushed before committing its offset. Pack streams are not cached; payload hashes have negligible locality.

## Check the server

```sh
dotnet run --project Kennedy.SmokeTests -- --settings /path/to/local-server.json
```

The JSON settings must point to the copied data, public root, certificate, and key. The runner starts the real server on an available high port, sends real TLS requests, saves responses and logs, and cleans up the server. It requires results for text and image searches for `cat` and checks the public tools, reports, stats, and capsule lists. Use `--capsule` and `--url` for another known capsule/page. Network tools contact that target; skipped tools are explicitly reported.

`--base-url gemini://host:port/` checks an already running instance. Normal builds compile the smoke runner without executing it. The runner does not upgrade databases or import content.

## Publish consistent data

Let indexing and post-processing finish, then stop all writers before preparing the publication copy. Use SQLite's backup API or `.backup` command to copy both databases, including committed WAL data, and copy `Packs/` and `archive-stats.json` from the same stopped dataset. Continue managing the public root, server settings, and certificates through the existing publication workflow.

Run the smoke test with settings pointing to the snapshot. Build/publish the server using the current host's existing architecture and deployment method. The existing Linux x64 self-contained build remains:

```sh
dotnet publish Server/Kennedy.Server.csproj -c Release -r linux-x64 --self-contained true -o /path/to/server-build
```

Stop the serving instance before replacing its database/pack set. Transfer the complete snapshot, the new binary, and the separately maintained public root/configuration. Preserve the Gemini certificate and key. Start Kennedy with settings whose DataRoot contains `kennedy2.db`, `archive.db`, and `Packs/`, then run smoke checks against the serving address.

Before the first replacement, preserve the previous binary and the complete previous dataset, including the legacy `doc-index.db`. A sync using `--delete` against a Kennedy2-only snapshot would otherwise remove that rollback file. To roll back, stop the server, restore the previous binary and matching complete data set, and restart. Keep historical WARCs and the rollback copy until the new deployment has been verified.

## Exit codes and recovery

- **Indexer:** 0 complete; 1 import or unrecoverable-without-source-content failure; 2 setup/finalization error; 130 interrupted.
- **Smoke runner:** 0 selected checks passed; 1 failed checks; 2 setup failure; 130 interrupted.
- Rerun the same original command to finish queued work after interruption. Records are reconsidered safely, and durable post-processing queues still drain.
- Missing archive packs must be restored from the matching archive backup. Missing search text can be recovered from its source WARCs. Do not discard the source data to hide an error.
