# Kennedy2 indexing and publication

Work on the `kennedy2` branch. `Kennedy-1` remains the old reference checkout. Builds require the sibling Gemini.Net, Warc.Net, and RocketForce projects, matching the existing repository layout.

## Import completed WARCs locally

```sh
dotnet run --project Indexer -- --db /path/to/crawl-data/kennedy2.db /path/to/new-crawl.warc.gz
```

By default this updates both search and the archive. `archive.db`, `Packs/`, and `archive-stats.json` are placed beside `kennedy2.db`; `--archive-dir` selects another archive directory. The language profile is packaged with the indexer. `--config-dir` overrides it.

Use `--warc-dir /path/to/WARCs` to select a directory, `--search-only` or `--archive-only` for one destination, and `--smoke-query cat` to print a post-import search sample. `--bootstrap` selects a directory with no filename-date requirement or age cutoff.

First run the new code against copies of the existing Kennedy2 data and archive packs. Do not substitute the legacy `doc-index.db`. The indexer creates missing Kennedy2 tables, columns, and indexes, consolidates duplicate document rows, repairs derived entries where possible, and finishes pending visibility/index work. It never rebuilds missing text without original response bodies: supply the corresponding WARCs when it reports missing text. Completed inputs are replayed automatically during that repair. `--force` handles other deliberate backfill.

The search index retains the latest usable content across temporary failures, with the actual successful capture date. Newer permanent failures, redirects, interactive/authentication responses, and policy exclusions stop that content appearing in search. Robots exclusions retain the payload internally so lifting the restriction can restore it. Archive visibility follows the `archiver` robots rules independently.

Files are considered immutable. Completion records use full path, length, and modification time and are stored separately in each destination. If a file fails or changes during import, it is not marked complete. Earlier committed batches remain safe to replay. Ctrl+C preserves completed batches and pending work. Writer lock files prevent overlapping indexers and can remain on disk after a successful run; the open file handle is the lock.

## Check the server

```sh
dotnet run --project Kennedy.SmokeTests -- --settings /path/to/local-server.json
```

The JSON settings must point to the copied data, public root, certificate, and key. The runner starts the real server on an available high port, sends real TLS requests, saves responses and logs, and cleans up the server. It requires results for text and image searches for `cat` and checks the public tools, reports, stats, and capsule lists. Use `--capsule` and `--url` for another known capsule/page. Network tools contact that target; skipped tools are explicitly reported.

`--base-url gemini://host:port/` checks an already running instance. Normal builds compile the smoke runner without executing it. The runner does not upgrade databases or import content.

## Publish consistent data

```sh
dotnet run --project Indexer -- --db /path/to/crawl-data/kennedy2.db --snapshot /path/to/new-snapshot
```

This finishes maintenance and produces a standalone copy of both databases, archive packs, and archive stats. It includes committed SQLite WAL data. It does not copy the public root, server configuration, or certificates; continue managing those with the existing publication workflow. Do not publish a `.tmp-…` directory.

Run the smoke test with settings pointing to the snapshot. Build/publish the server using the current host's existing architecture and deployment method. The existing Linux x64 self-contained build remains:

```sh
dotnet publish Server/Kennedy.Server.csproj -c Release -r linux-x64 --self-contained true -o /path/to/server-build
```

Stop the serving instance before replacing its database/pack set. Transfer the complete snapshot, the new binary, and the separately maintained public root/configuration. Preserve the Gemini certificate and key. Start Kennedy with settings whose DataRoot contains `kennedy2.db`, `archive.db`, and `Packs/`, then run smoke checks against the serving address.

Before the first replacement, preserve the previous binary and the complete previous dataset, including the legacy `doc-index.db`. A sync using `--delete` against a Kennedy2-only snapshot would otherwise remove that rollback file. To roll back, stop the server, restore the previous binary and matching complete data set, and restart. Keep historical WARCs and the rollback copy until the new deployment has been verified.

## Exit codes and recovery

- **Indexer:** 0 complete; 1 import or unrecoverable-without-source-content failure; 2 setup/finalization error; 130 interrupted.
- **Smoke runner:** 0 selected checks passed; 1 failed checks; 2 setup failure; 130 interrupted.
- Use `--maintenance` to finish queued work without new input. Scope it with `--search-only` or `--archive-only` when appropriate.
- Missing archive packs must be restored from the matching archive backup. Missing search text can be recovered from its source WARCs. Do not discard the source data to hide an error.
