# Kennedy 2.0 — Architecture Reference

> Target audience: a mid-senior C# developer who has never seen this codebase. After reading
> this document you should have enough understanding to implement the system from scratch.

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [External Dependencies and Sibling Libraries](#2-external-dependencies-and-sibling-libraries)
3. [Solution Structure](#3-solution-structure)
4. [Architectural Overview Diagram](#4-architectural-overview-diagram)
5. [Database Schema](#5-database-schema)
6. [Parsing Pipeline](#6-parsing-pipeline)
7. [Response Store](#7-response-store-kennedydataservicesresponsestorecs)
8. [FTS Index Management](#8-fts-index-management)
9. [Archive System](#9-archive-system-kennedyarchive)
10. [Search Layer](#10-search-layer-kennedysearch)
11. [WARC Writing](#11-warc-writing-kennedywarc)
12. [robots.txt Support](#12-robotstxt-support)
13. [Indexer CLI](#13-indexer-cli)
14. [Server](#14-server-kennedyserver)
15. [Key Implementation Notes and Gotchas](#15-key-implementation-notes-and-gotchas)
16. [Step-by-Step Implementation Checklist](#16-step-by-step-implementation-checklist)

---

## 1. Introduction

Kennedy 2.0 is a **full-text search engine for the Gemini protocol**. Gemini is a lightweight
alternative internet protocol (similar in spirit to Gopher) that uses a simple request/response
format over TLS on port 1965. Responses can be Gemtext (the native markup format), plain text,
images, or arbitrary binary data.

Kennedy does **not** contain a crawler. An external crawler (not in this repo) fetches Gemini
capsules (sites) and writes the results to WARC files — the same archival container format used
by the Internet Archive. Kennedy's job is to:

1. **Ingest** those WARC files.
2. **Parse** each Gemini response (detect MIME type, extract text, follow links, detect language).
3. **Store** parsed content in a SQLite search database (the "search DB").
4. **Maintain** a full-text search index (SQLite FTS5) automatically via triggers.
5. **Separately archive** raw response bytes in a content-addressed pack-file store for historical browsing.
6. **Serve** search queries over the Gemini protocol (the Server project).

The technology stack is **.NET 8**, **SQLite** (via Microsoft.Data.Sqlite and EF Core 9), and a handful
of NuGet packages for image inspection and language detection.

---

## 2. External Dependencies and Sibling Libraries

The solution references two sibling repositories that live outside this repo. You must clone them
at the expected relative paths.

| Library | Path (relative to this repo) | Purpose |
|---|---|---|
| `Gemini.Net` | `../../Gemini.Net/src/Gemini.Net.csproj` | Gemini protocol primitives: `GeminiUrl`, `GeminiResponse`, `GeminiParser` |
| `Warc.Net` | `../../Warc.Net/Warc.Net.csproj` | WARC file reader/writer (`WarcReader`, `WarcWriter`, record types) |
| `RocketForce` | `../../RocketForce/RocketForce/RocketForce.csproj` | Gemini server framework used by the Server project |

### Key types from `Gemini.Net`

| Type | What it does |
|---|---|
| `GeminiUrl` | Parses and normalises a Gemini URL. `NormalizedUrl` is the canonical string form. `MakeUrl(base, relative)` resolves relative URLs. `ID` is a stable long hash of the URL. |
| `GeminiResponse` | A parsed Gemini response: `StatusCode`, `Meta`, `MimeType`, `BodyBytes`, `BodyText`, `IsSuccess`, `IsRedirect`, `HasBody`, `Hash` (SHA-256 hex of body), `RequestSent`, `ResponseReceived`. |
| `GeminiParser` | Static helpers: `ParseResponseBytes`, `CreateResponseBytes`, `CreateRequestBytes`, `GetStrongHash`, `IsSuccessStatus`, `IsRedirectStatus`, `IsInputStatus`, `IsTempFailStatus`, `IsPermFailStatus`, `ConnectionErrorStatusCode`. |
| `TlsConnectionInfo` | TLS connection metadata attached to a response: protocol version, cipher suite, remote certificate. |

### Key NuGet packages in `Kennedy.Data`

| Package | Use |
|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` 9.x | ORM for the search SQLite database |
| `SixLabors.ImageSharp` 3.x | Image dimension and alpha channel detection |
| `FileSignatures` 5.x | Magic-byte format detection (detects images, PDFs, ZIP, etc.) |
| `NTextCat` 0.3.65 | Language detection using n-gram models (Core14 language profile) |

---

## 3. Solution Structure

```
Kennedy.sln
├── Kennedy.Data/          Class library — core domain models, EF Core DbContext, parsers, storage services
├── Kennedy.Search/        Class library — search models, query parsing, FTS5 query execution
├── Kennedy.Archive/       Class library — WARC-style content-addressed archive (pack files + SQLite)
├── Warc/                  Class library (Kennedy.Warc) — WARC file writing (GeminiWarcCreator)
├── Indexer/               Console application — reads WARC files and drives the ResponseStore
├── Server/                Gemini server application — serves search queries over Gemini protocol
└── Kennedy.Tests/         Test harness (minimal at time of writing)
```

### Project dependency graph

```
Indexer ──────────────────────────────────────────────────────────────────┐
  ├── Kennedy.Data                                                         │
  │     └── Gemini.Net                                                     │
  ├── Kennedy.Warc                                                         │
  │     ├── Gemini.Net                                                     │
  │     └── Warc.Net                                                       │
  └── Warc.Net                                                             │
                                                                           │
Server ────────────────────────────────────────────────────────────────────┤
  ├── Kennedy.Search                                                       │
  │     ├── Kennedy.Data                                                   │
  │     └── Gemini.Net                                                     │
  ├── Kennedy.Data                                                         │
  ├── Kennedy.Archive                                                      │
  │     ├── Gemini.Net                                                     │
  │     └── Kennedy.Data                                                   │
  └── RocketForce                                                          │
                                                                           ▼
                                           (runtime: kennedy2.db + pack files)
```

---

## 4. Architectural Overview Diagram

The diagram below shows the full data flow from crawler output through every subsystem to end-user
search queries. Read it top-to-bottom.

```
╔══════════════════════════════════════════════════════════════════════════════════════════════╗
║                            KENNEDY 2.0 — DATA FLOW DIAGRAM                                   ║
╚══════════════════════════════════════════════════════════════════════════════════════════════╝

  [External Crawler]
       │  writes raw Gemini request+response bytes (TLS session data, certs)
       ▼
  ┌──────────────────────┐
  │  WARC File(s)        │  (.warc or .warc.gz)
  │  ─────────────────── │  Each file holds thousands of WARC records.
  │  WarcInfoRecord      │  ResponseRecord.ContentBlock = raw Gemini response bytes
  │  RequestRecord       │  RequestRecord.ContentBlock  = raw Gemini request bytes
  │  ResponseRecord ──── │  ResponseRecord.TargetUri    = gemini:// URL
  │  MetadataRecord      │  MetadataRecord              = PEM certificate
  └──────────┬───────────┘
             │
             │  [Indexer/WarcIndexer.IndexFileAsync]
             │  WarcDotNet.WarcReader iterates records
             │  Only ResponseRecord with scheme=gemini are processed
             │  GeminiParser.ParseResponseBytes(url, bytes) → GeminiResponse
             │
             ▼
  ┌──────────────────────────────────────────────────────────────────────┐
  │                     PARSING PIPELINE                                  │
  │           Kennedy.Data.Parsers.ResponseParser.Parse(GeminiResponse)  │
  │                                                                       │
  │  1. TryParseRedirect ──── if IsRedirect → extract redirect URL       │
  │         │                                  return ParsedResponse      │
  │         │ (not redirect)                   with Links=[redirect URL]  │
  │         ▼                                                             │
  │  2. IsSuccess && HasBody? ── No ──► bare ParsedResponse (no content) │
  │         │ Yes                                                         │
  │         ▼                                                             │
  │  3. BinaryParser.Parse                                                │
  │     FileSignatures.FileFormatInspector (magic bytes)                 │
  │         │                                                             │
  │         ├─ image detected ──► ImageSharp.Image.Identify              │
  │         │                     ──► ImageResponse (Width, Height,       │
  │         │                          ImageType, IsTransparent)          │
  │         │                                                             │
  │         ├─ other binary ───► ParsedResponse {FormatType=Binary}      │
  │         │                                                             │
  │         └─ not detected ──► falls through to TextParser              │
  │                                                                       │
  │  4. TextParser.Parse                                                  │
  │     MimeSniffer.IsText (WHATWG, checks first 1445 bytes)             │
  │         │                                                             │
  │         ├─ GemTextResponseParser.CanParse: MimeType=text/gemini      │
  │         │    LineParser.GetLines (split on \n)                        │
  │         │    LineParser.RemovePreformattedLines (strip ``` blocks)    │
  │         │    GetIndexableContent (link lines → link text only)        │
  │         │    LinkFinder.GetLinks (regex ^=>\s*([^\s]+)\s*(.*))        │
  │         │    TitleFinder.FindTitle (first heading or pre alt text)    │
  │         │    HashtagsFinder.GetHashtags                               │
  │         │    MentionsFinder.GetMentions                               │
  │         │    LanguageDetector.DetectLanguage (NTextCat, Core14)       │
  │         │    IsGemFeed (≥2 links with ISO 8601 date text)            │
  │         │    ──► GemTextResponse                                      │
  │         │                                                             │
  │         ├─ PlainTextResponseParser.CanParse: MimeType=text/plain     │
  │         │    LanguageDetector.DetectLanguage                          │
  │         │    ──► PlainTextResponse (IndexableText = BodyText)         │
  │         │                                                             │
  │         └─ neither matches ──► fallback ParsedResponse {Binary}      │
  └──────────────────────────┬───────────────────────────────────────────┘
                             │  ParsedResponse (one of the subtypes above)
                             │
             ┌───────────────┴─────────────────────────────┐
             │                                             │
             ▼                                             ▼
  ┌──────────────────────┐                   ┌────────────────────────────┐
  │  RESPONSE STORE      │                   │  ARCHIVE SYSTEM            │
  │  ResponseStore       │                   │  Kennedy.Archive           │
  │  .StoreResponseAsync │                   │  Archiver.ArchiveResponse  │
  │                      │                   │                            │
  │  (one SQLite tx)     │                   │  Filter: success+body,     │
  │                      │                   │  redirect, input, auth     │
  │  ┌─────────────────┐ │                   │  Skip: non-text >5MB or    │
  │  │  UrlRegistry    │ │                   │  truncated non-text        │
  │  │  (upsert)       │ │                   │                            │
  │  │  ApplyLifecycle │ │                   │  SHA-256 hash of bytes     │
  │  │  ApplyComponents│ │                   │  ──► dedup check in        │
  │  └────────┬────────┘ │                   │      Snapshots table       │
  │           │          │                   │                            │
  │  ┌────────▼────────┐ │                   │  New hash ──► PackManager  │
  │  │  Documents      │ │                   │  .GetPack(hash)            │
  │  │  (upsert)       │ │                   │  PackRecordFactory         │
  │  │  ResponseHash   │ │                   │  .MakeOptimalRecord        │
  │  │  unchanged?     │ │                   │  (try gzip, <90% = DATZ)   │
  │  │  ─ yes: skip FTS│ │                   │  PackFile.Append           │
  │  │  ─ no: full upd │ │                   │                            │
  │  └────────┬────────┘ │                   │  Insert Snapshot row       │
  │           │          │                   │  (Offset, DataHash, ...)   │
  │  ┌────────▼────────┐ │                   └────────────┬───────────────┘
  │  │  UrlLinks       │ │                                │
  │  │  (delete+insert)│ │                   ┌────────────▼───────────────┐
  │  └─────────────────┘ │                   │  Pack Files on Disk        │
  └──────────┬───────────┘                   │  <root>/<c0><c1>/          │
             │  SaveChanges                  │          <c2><c3>/         │
             │  ──► SQLite FTS5 triggers     │          <c0><c1><c2><c3>  │
             │      fire automatically       │  (2-level dir by hash)     │
             │                               └────────────────────────────┘
             │
  ┌──────────▼───────────────────────────────────────────────────────────┐
  │                     SQLITE DATABASE  (kennedy2.db)                    │
  │                                                                       │
  │  ┌─────────────┐   ┌──────────────────┐   ┌──────────────────────┐  │
  │  │ UrlRegistry │   │ Documents        │   │ DocumentImages       │  │
  │  │─────────────│   │──────────────────│   │──────────────────────│  │
  │  │ Id (PK)     │◄──│ UrlRegistryId FK │   │ DocumentId (PK+FK)   │  │
  │  │ NormalizedUrl│  │ NormalizedUrl UQ │   │ Width, Height        │  │
  │  │ Scheme,Host │   │ CanonicalUrl     │   │ ImageType            │  │
  │  │ Port,Path   │   │ Title, Content   │   │ IsTransparent        │  │
  │  │ Status      │   │ MimeType         │   └──────────────────────┘  │
  │  │ FirstSeen   │   │ ResponseHash     │         ▲ CASCADE DELETE     │
  │  │ LastVisit   │   │ BodyHash         │         │                    │
  │  │ IsImage     │   │ IsSearchable     │   ┌─────┴────────────────┐  │
  │  │ ImageWidth  │   │ ContentType      │   │  Documents (cont.)   │  │
  │  │ ImageType   │   │ IsFeed           │   │  Image nav.prop.     │  │
  │  │ ...         │   │ OutboundLinks    │   └──────────────────────┘  │
  │  └──────┬──────┘   │ Language         │                             │
  │         │          │ DetectedLanguage │   ┌──────────────────────┐  │
  │         │          └────────┬─────────┘   │ UrlLinks             │  │
  │         │                   │             │──────────────────────│  │
  │         │          ┌────────▼─────────┐   │ Id (PK)              │  │
  │         │          │ DocumentsFts     │   │ SourceUrlId (FK)     │  │
  │         │          │  (FTS5 virtual)  │   │ TargetUrlId (FK)     │  │
  │         │          │──────────────────│   │ IsExternal           │  │
  │         │          │ Title            │   │ LinkText             │  │
  │         │          │ Content          │   └──────────────────────┘  │
  │         │          │ CanonicalUrl     │                             │
  │         │          │ content=Documents│   ┌──────────────────────┐  │
  │         │          │ content_rowid=Id │   │ FilesFts             │  │
  │         │          │                  │   │  (FTS5 virtual)      │  │
  │         │          │ TRIGGERS:        │   │──────────────────────│  │
  │         │          │ Documents_ai     │   │ UrlRegistryId UNINDX │  │
  │         └──────────┤ Documents_ad     │   │ SearchText           │  │
  │    IsImage=1       │ Documents_au     │   │ (rebuilt in bulk     │  │
  │                    └──────────────────┘   │  after each ingest)  │  │
  │                                           └──────────────────────┘  │
  └──────────────────────────────────────────────────────────────────────┘
             │                   │
             │                   │  [After all WARCs processed]
             │                   │
             │          ┌────────▼────────────────────────────────────────┐
             │          │  FileSearchFtsRebuilder.RebuildAsync            │
             │          │                                                  │
             │          │  DELETE FROM FilesFts                            │
             │          │  SELECT non-text URLs w/ success status         │
             │          │  JOIN UrlLinks → link texts per target           │
             │          │  Concatenate: filename/path + link texts         │
             │          │  Normalize (strip non-alphanum, lowercase)       │
             │          │  INSERT INTO FilesFts                            │
             │          └──────────────────────────────────────────────────┘
             │
             ▼
  ┌──────────────────────────────────────────────────────────────────────┐
  │                   SEARCH LAYER  (Kennedy.Search)                      │
  │                                                                       │
  │  UserQuery (RawQuery → QueryParser)                                  │
  │    ├─ Extract: site:domain, filetype:ext,                            │
  │    │           intitle:term, inurl:pattern                           │
  │    └─ Remaining terms → FtsSyntaxConverter.Convert                   │
  │         bare words  → "word" (implicit double-quotes)                │
  │         AND/OR/NOT  → preserved as FTS5 boolean operators            │
  │         'single q'  → '' (escaped)                                   │
  │         "phrases"   → pass through as FTS5 phrase queries            │
  │                                                                       │
  │  SqliteSearchService                                                  │
  │  ┌────────────────────────────────────────────────────────────────┐  │
  │  │ SearchText(query, offset, limit)                               │  │
  │  │   SELECT d.CanonicalUrl, d.Title, snippet(DocumentsFts,...),   │  │
  │  │          d.MimeType, d.DetectedLanguage, ...                   │  │
  │  │   FROM Documents d                                             │  │
  │  │   INNER JOIN DocumentsFts ON DocumentsFts.rowid = d.Id        │  │ 
  │  │   WHERE d.IsSearchable = 1                                     │  │
  │  │     AND DocumentsFts MATCH @fts_query                          │  │
  │  │     [AND d.CanonicalUrl LIKE 'gemini://host/%']  ← site:       │  │
  │  │     [AND d.MimeType LIKE '%ext%']                ← filetype:   │  │
  │  │     [AND d.Title LIKE '%term%']                  ← intitle:    │  │
  │  │     [AND d.CanonicalUrl LIKE '%pattern%']        ← inurl:      │  │
  │  │   ORDER BY d.LastIndexedUtc DESC                               │  │
  │  └────────────────────────────────────────────────────────────────┘  │
  │  ┌────────────────────────────────────────────────────────────────┐  │
  │  │ SearchImages(query, offset, limit)                             │  │
  │  │   SELECT u.NormalizedUrl, u.ImageType, u.ImageWidth/Height,    │  │
  │  │          snippet(FilesFts,...)                                  │  │
  │  │   FROM UrlRegistry u                                           │  │
  │  │   INNER JOIN FilesFts ON FilesFts.rowid = u.Id                 │  │
  │  │   WHERE u.IsImage = 1                                          │  │
  │  │     AND FilesFts MATCH @fts_query                              │  │
  │  │   ORDER BY u.LastVisit DESC                                    │  │
  │  └────────────────────────────────────────────────────────────────┘  │
  └──────────────────────────────────────────────────────────────────────┘
             │
             ▼
  ┌──────────────────────────────────────────────────────────────────────┐
  │                     GEMINI SERVER  (Kennedy.Server)                   │
  │                     RocketForce framework                             │
  │                                                                       │
  │  Routes (all Gemini paths):                                           │
  │  /search              → SearchController.Search                       │
  │  /image-search        → ImageSearchController.Search                  │
  │  /archive/cached      → ArchiveController.Cached                      │
  │  /archive/history     → ArchiveController.UrlHistory                  │
  │  /archive/diff        → ArchiveController.Diff                        │
  │  /reports/...         → ReportsController                             │
  │  /certs/...           → CertsController                               │
  │  /tools/...           → ToolsController                               │
  └──────────────────────────────────────────────────────────────────────┘
```

---

## 5. Database Schema

There are **two separate SQLite databases**:

| Database | Used by | Contains |
|---|---|---|
| `kennedy2.db` | Indexer, Server, Kennedy.Data, Kennedy.Search | Search index (URL registry, documents, FTS, links) |
| `archive.db` | Kennedy.Archive | Archive snapshot metadata (URLs + Snapshots) |

Pack files (raw response bytes) live in a directory tree alongside `archive.db`.

### 5.1 kennedy2.db — Search Database

Managed by `KennedyDbContext` (EF Core). Schema created with `EnsureCreated()` plus a mandatory
separate call to `EnsureFtsAsync()` for the virtual tables.

#### UrlRegistry

The **authoritative URL table**. Every URL the system has ever seen gets exactly one row, regardless
of how many times it has been fetched. The indexer writes to this table; the crawler scheduler reads
it to decide what to fetch next.

```sql
CREATE TABLE UrlRegistry (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    NormalizedUrl    TEXT NOT NULL,         -- canonical URL (max 1024)
    Scheme           TEXT NOT NULL,         -- always "gemini" in practice (max 16)
    Host             TEXT NOT NULL,         -- lowercase hostname (max 255)
    Port             INTEGER NOT NULL,      -- default 1965
    PathAndQuery     TEXT NOT NULL,         -- e.g. "/path?q=x" (max 1024)
    FileName         TEXT,                  -- filename portion of path (max 256)
    FirstSeen        TEXT NOT NULL,         -- ISO-8601 UTC datetime
    LastVisit        TEXT,                  -- last attempt (success or fail)
    LastSuccess      TEXT,                  -- last successful fetch (2x, redirect, input)
    LastContentChange TEXT,                 -- last time body hash differed
    LastContentHash  TEXT,                  -- SHA-256 of last successful body
    LastStatusCode   INTEGER,               -- raw Gemini status code (20, 51, etc.)
    Status           INTEGER NOT NULL,      -- UrlStatus enum (see below)
    SuccessCount     INTEGER NOT NULL DEFAULT 0,
    FailureCount     INTEGER NOT NULL DEFAULT 0,
    PriorityScore    REAL NOT NULL DEFAULT 0.0,
    Meta             TEXT NOT NULL DEFAULT '', -- redirect target or input prompt
    LastMimeType     TEXT,                  -- MIME type from last response header
    LastDetectedMimeType TEXT,              -- MIME detected from magic bytes
    IsTextDocument   INTEGER NOT NULL DEFAULT 0,  -- BOOLEAN
    IsImage          INTEGER NOT NULL DEFAULT 0,  -- BOOLEAN
    ImageWidth       INTEGER,
    ImageHeight      INTEGER,
    ImageType        TEXT                   -- "Png", "Jpeg", etc. (max 64)
);
CREATE UNIQUE INDEX IX_UrlRegistry_NormalizedUrl ON UrlRegistry (NormalizedUrl);
CREATE INDEX IX_UrlRegistry_Status_Priority     ON UrlRegistry (Status, PriorityScore);
CREATE INDEX IX_UrlRegistry_Host                ON UrlRegistry (Host);
```

**UrlStatus enum values** (stored as integer):

| Value | Name | Meaning |
|---|---|---|
| 0 | New | Discovered but never fetched |
| 1 | Active | Last fetch returned 20 (success) |
| 2 | TemporaryError | 4x / 5x Gemini status, or transient failure |
| 3 | ConnectionError | Network / TLS / DNS failure (no protocol status) |
| 4 | PermanentError | Definitively unreachable |
| 5 | Gone | 51 (Not Found) |
| 6 | Redirect | Always redirects (30/31) |
| 7 | ExcludedByRobots | Blocked by robots.txt rules |
| 8 | DenyList | Operator deny-listed |
| 9 | LowValueSuppressed | Auto-suppressed templated URL |
| 10 | RemovedByOwnerRequest | Owner takedown |
| 11 | ManuallyDisabled | Operator disabled |
| 12 | Interactive | Returns Gemini 1x (input required) |
| 99 | UNKNOWN | Unrecognized status |

#### Documents

One row per URL that has **indexable text content**. Non-text URLs (images, binary) do NOT have a
row here. When a URL transitions from text → non-text, the document row is deleted.

```sql
CREATE TABLE Documents (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    UrlRegistryId    INTEGER REFERENCES UrlRegistry(Id) ON DELETE SET NULL,
    NormalizedUrl    TEXT NOT NULL,         -- same as UrlRegistry.NormalizedUrl
    CanonicalUrl     TEXT NOT NULL,         -- currently same as NormalizedUrl
    Title            TEXT,                  -- first heading or preformatted alt-text (max 512)
    Content          TEXT NOT NULL,         -- full indexable text (may be large)
    MimeType         TEXT,                  -- from response header (max 256)
    DetectedMimeType TEXT,                  -- from magic bytes (max 256)
    StatusCode       INTEGER NOT NULL,
    BodyHash         TEXT,                  -- SHA-256 of body bytes (max 128)
    ResponseHash     TEXT,                  -- SHA-256 of entire response (max 128)
    LastIndexedUtc   TEXT NOT NULL,
    IsSearchable     INTEGER NOT NULL DEFAULT 0,  -- BOOLEAN
    IsBodyTruncated  INTEGER NOT NULL DEFAULT 0,  -- BOOLEAN
    BodySize         INTEGER NOT NULL DEFAULT 0,
    OutboundLinks    INTEGER NOT NULL DEFAULT 0,
    IsFeed           INTEGER NOT NULL DEFAULT 0,  -- BOOLEAN (Gemfeed heuristic)
    LineCount        INTEGER,
    Language         TEXT,                  -- from response header (max 8)
    DetectedLanguage TEXT,                  -- ISO 639-1 two-letter code (max 8)
    ContentType      INTEGER NOT NULL DEFAULT 0   -- ContentType enum
);
CREATE UNIQUE INDEX IX_Documents_NormalizedUrl ON Documents (NormalizedUrl);
CREATE INDEX IX_Documents_IsSearchable         ON Documents (IsSearchable);
CREATE INDEX IX_Documents_StatusCode           ON Documents (StatusCode);
```

**ContentType enum values** (stored as integer):

| Value | Name |
|---|---|
| 0 | Unknown |
| 1 | Gemtext |
| 2 | Image |
| 3 | Binary |
| 4 | PlainText |

#### DocumentImages

Stores image metadata for image URLs. The primary key is a shared PK/FK with Documents — the
values are identical. This is a 1:1 relationship with cascade delete.

```sql
CREATE TABLE DocumentImages (
    DocumentId   INTEGER PRIMARY KEY REFERENCES Documents(Id) ON DELETE CASCADE,
    Width        INTEGER NOT NULL,
    Height       INTEGER NOT NULL,
    ImageType    TEXT NOT NULL,     -- "Png", "Jpeg", "Gif", "Webp", etc. (max 64)
    IsTransparent INTEGER NOT NULL DEFAULT 0  -- BOOLEAN
);
```

#### UrlLinks

Directed link graph. Every link on a Gemtext/plain-text page that points to a Gemini URL
generates a row. Rows are deleted and re-inserted every time the source page is re-indexed.

```sql
CREATE TABLE UrlLinks (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    SourceUrlId INTEGER NOT NULL REFERENCES UrlRegistry(Id),
    TargetUrlId INTEGER NOT NULL REFERENCES UrlRegistry(Id),
    IsExternal  INTEGER NOT NULL DEFAULT 0,  -- BOOLEAN: different authority than source
    LinkText    TEXT NOT NULL DEFAULT ''     -- display text from the link line (max 512)
);
CREATE INDEX IX_UrlLinks_SourceUrlId ON UrlLinks (SourceUrlId);
CREATE INDEX IX_UrlLinks_TargetUrlId ON UrlLinks (TargetUrlId);
```

#### DocumentsFts (FTS5 virtual table)

Content-table FTS5 index backed by the Documents table. Created and maintained by
`KennedyDbContext.EnsureFtsAsync()` — **not** by EF Core migrations.

```sql
CREATE VIRTUAL TABLE IF NOT EXISTS DocumentsFts USING fts5(
    Title,
    Content,
    CanonicalUrl,
    content='Documents',
    content_rowid='Id'
);
```

Three SQLite triggers keep it synchronised:

```sql
-- AFTER INSERT
CREATE TRIGGER IF NOT EXISTS Documents_ai AFTER INSERT ON Documents BEGIN
    INSERT INTO DocumentsFts(rowid, Title, Content, CanonicalUrl)
    VALUES (new.Id, new.Title, new.Content, new.CanonicalUrl);
END;

-- AFTER DELETE
CREATE TRIGGER IF NOT EXISTS Documents_ad AFTER DELETE ON Documents BEGIN
    INSERT INTO DocumentsFts(DocumentsFts, rowid, Title, Content, CanonicalUrl)
    VALUES ('delete', old.Id, old.Title, old.Content, old.CanonicalUrl);
END;

-- AFTER UPDATE (delete old entry, insert new entry)
CREATE TRIGGER IF NOT EXISTS Documents_au AFTER UPDATE ON Documents BEGIN
    INSERT INTO DocumentsFts(DocumentsFts, rowid, Title, Content, CanonicalUrl)
    VALUES ('delete', old.Id, old.Title, old.Content, old.CanonicalUrl);
    INSERT INTO DocumentsFts(rowid, Title, Content, CanonicalUrl)
    VALUES (new.Id, new.Title, new.Content, new.CanonicalUrl);
END;
```

#### FilesFts (FTS5 virtual table)

Enables full-text search over non-text files (images, binaries) by the link text that points to
them and tokens extracted from the URL path. **Not trigger-maintained** — rebuilt from scratch
by `FileSearchFtsRebuilder` after each ingestion run.

```sql
CREATE VIRTUAL TABLE IF NOT EXISTS FilesFts USING fts5(
    UrlRegistryId UNINDEXED,   -- stored but not indexed (used for joining back)
    SearchText                 -- concatenation of link texts + URL path tokens
);
```

### 5.2 archive.db — Archive Database

Managed by `ArchiveDbContext` in `Kennedy.Archive`. Created with standard `EnsureCreated()`.

#### Urls (archive)

```sql
CREATE TABLE Urls (
    Id       INTEGER PRIMARY KEY,  -- same value as GeminiUrl.ID
    FullUrl  TEXT NOT NULL,
    Domain   TEXT NOT NULL,
    Protocol TEXT NOT NULL,
    Port     INTEGER NOT NULL DEFAULT 1965,
    IsPublic INTEGER NOT NULL DEFAULT 1   -- BOOLEAN
);
CREATE INDEX IX_Urls_Domain   ON Urls (Domain);
CREATE INDEX IX_Urls_Port     ON Urls (Port);
CREATE INDEX IX_Urls_Protocol ON Urls (Protocol);
```

Note: the `Url.Id` is seeded from `GeminiUrl.ID` (a hash), not an auto-increment.

#### Snapshots

```sql
CREATE TABLE Snapshots (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    UrlId           INTEGER NOT NULL REFERENCES Urls(Id),
    Captured        TEXT NOT NULL,         -- UTC capture time
    StatusCode      INTEGER NOT NULL,
    DataHash        TEXT NOT NULL,         -- "sha-256:abcdef..." — hash of raw bytes
    Mimetype        TEXT,
    Offset          INTEGER NOT NULL,      -- byte offset in pack file
    Size            INTEGER NOT NULL,      -- uncompressed size of response bytes
    IsDuplicate     INTEGER NOT NULL DEFAULT 0,      -- same content, same URL
    IsGlobalDuplicate INTEGER NOT NULL DEFAULT 0,    -- same content, different URL
    HasBodyContent  INTEGER NOT NULL DEFAULT 0,
    IsBodyTruncated INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IX_Snapshots_DataHash  ON Snapshots (DataHash);
CREATE INDEX IX_Snapshots_UrlId     ON Snapshots (UrlId);
CREATE INDEX IX_Snapshots_Captured  ON Snapshots (Captured);
```

---

## 6. Parsing Pipeline

All parsing is initiated by `ResponseParser.Parse(GeminiResponse)` in
`Kennedy.Data/Parsers/ResponseParser.cs`. The pipeline returns a `ParsedResponse` (or a subtype).

### 6.1 Class hierarchy

```
GeminiResponse  (Gemini.Net — base; holds raw response data)
    └── ParsedResponse  (Kennedy.Data — adds FormatType, DetectedMimeType, Links)
            ├── ImageResponse     (adds Width, Height, ImageType, IsTransparent)
            ├── GemTextResponse   (implements ITextResponse; adds Title, IndexableText,
            │                      IsFeed, HashTags, Mentions, DetectedLanguage, LineCount)
            └── PlainTextResponse (implements ITextResponse; IndexableText = BodyText)
```

`ITextResponse` is the key interface:

```csharp
public interface ITextResponse
{
    string? DetectedLanguage { get; }
    bool HasIndexableText { get; }
    bool IsFeed { get; }
    string? IndexableText { get; }
    int LineCount { get; }
    string? Title { get; }
}
```

`HasIndexableText` on `PlainTextResponse` also checks `IsProactiveRequest` (suppresses
`/robots.txt`, `/favicon.txt`, `/.well-known/security.txt` from being indexed).

### 6.2 ResponseParser decision tree

```
ResponseParser.Parse(GeminiResponse)
│
├─ TryParseRedirect?
│    IsRedirect == true
│    FoundLink.Create(requestUrl, meta) → link to redirect target
│    return ParsedResponse { Links = [redirectLink] }
│
├─ !IsSuccess || !HasBody
│    return bare ParsedResponse (no content, no links)
│
├─ BinaryParser.Parse(resp)
│    inspector.DetermineFileFormat(BodyBytes)  ← magic bytes
│    │
│    ├─ null (no known format)
│    │    return null → TextParser gets a chance
│    │
│    ├─ FileSignatures.Formats.Image
│    │    Image.Identify(BodyBytes)             ← ImageSharp
│    │    return ImageResponse { Width, Height, ImageType, IsTransparent }
│    │    (on ImageSharp error: fall back to generic binary ParsedResponse)
│    │
│    └─ other known binary format
│         return ParsedResponse { FormatType=Binary, DetectedMimeType=format.MediaType }
│
├─ TextParser.Parse(resp)
│    sniffer.IsText(BodyBytes)
│      ─ scans first 1445 bytes
│      ─ UTF-16 BOM (0xFE 0xFF or 0xFF 0xFE) → true
│      ─ UTF-8 BOM (0xEF 0xBB 0xBF) → true
│      ─ any "binary byte" → false
│    │
│    ├─ GemTextResponseParser.CanParse: isTextBody && MimeType.StartsWith("text/gemini")
│    │    LineParser.GetLines(BodyText)                  → string[]
│    │    LineParser.RemovePreformattedLines(lines)      → List<string> (strips ``` blocks)
│    │    GetIndexableContent(noPreformatted)
│    │        for each line:
│    │            if starts with "=>" → extract link text only (not the URL)
│    │            else → keep the line as-is
│    │    LinkFinder.GetLinks(requestUrl, noPreformatted)
│    │        regex: ^=>\s*([^\s]+)\s*(.*)
│    │        skip: links containing "://" that are NOT "gemini://"
│    │        FoundLink.Create(pageUrl, rawUrl, linkText) → resolves relative URLs
│    │    TitleFinder.FindTitle(lines)          → first heading (^(#+)\s*(.+))
│    │                                            fallback: first ``` alt text
│    │    HashtagsFinder.GetHashtags             → regex [\,\s]#([a-zA-Z0-9][a-zA-Z0-9_\-]+)
│    │                                            excludes: all-numeric, CSS hex (3 or 6 hex chars)
│    │    MentionsFinder.GetMentions             → regex [\s\,][\@\~]([a-zA-Z_][a-zA-Z\d_\-]{2,})\s
│    │                                            or ^[\@\~]([...]) at start of line
│    │    LanguageDetector.DetectLanguage(indexableText)
│    │        min 150 chars; cap at 4096; NTextCat Core14.profile.xml
│    │        returns ISO 639-1 two-letter code (e.g. "en", "de") or null
│    │    IsGemFeed: ≥2 links where LinkText.Length≥10 AND matches ^\d{4}\-[01]\d\-[0123]\d
│    │    return GemTextResponse { ... }
│    │
│    └─ PlainTextResponseParser.CanParse: isTextBody && MimeType == "text/plain"
│         LanguageDetector.DetectLanguage(BodyText)
│         return PlainTextResponse { DetectedLanguage, IndexableText=BodyText }
│
└─ Fallback
     return ParsedResponse { FormatType=Binary }
```

### 6.3 FoundLink

`FoundLink` represents one resolved hyperlink extracted from a response:

- `Url` — resolved `GeminiUrl` (absolute)
- `IsExternal` — `newUrl.Authority != pageUrl.Authority`
- `LinkText` — display text from the link line

**Equality** is based solely on `Url` (not `LinkText`), so the same URL appearing twice on a page
with different link texts counts as one distinct link. `GetHashCode()` delegates to `Url`.

`FoundLink.Create(pageUrl, foundUrl, linkText)` calls `GeminiUrl.MakeUrl(pageUrl, foundUrl)`:
- If `foundUrl` contains `://` and it's not `gemini://`, returns null (ignored)
- If `foundUrl` is relative (no scheme), resolves against `pageUrl`
- Returns null if resolution fails or results in a non-Gemini URL

---

## 7. Response Store (`Kennedy.Data/Services/ResponseStore.cs`)

`ResponseStore.StoreResponseAsync(GeminiResponse, CancellationToken)` is the **single entry point
for writing to the search database**. It is called once per WARC record. All database writes happen
inside a single SQLite transaction for atomicity.

### 7.1 Full sequence

```
StoreResponseAsync(response, ct)
│
│  Parse response → ParsedResponse
│  Open DbContext via factory (IDbContextFactory<KennedyDbContext>)
│  BEGIN TRANSACTION
│
│  1. Look up UrlRecord by NormalizedUrl
│     ─ not found → create new UrlRecord(url) { Status=New, FirstSeen=visitTime }
│
│  2. ApplyUrlLifecycle(urlRecord, response, visitTime)
│     Sets LastVisit, LastStatusCode, then branches by status:
│
│     IsSuccess (2x)
│     ├── LastSuccess = visitTime, SuccessCount++, Meta = ""
│     ├── if response.Hash != LastContentHash:
│     │       LastContentHash = response.Hash
│     │       LastContentChange = visitTime
│     └── Status = Active
│
│     ConnectionErrorStatusCode
│     └── FailureCount++, Status = ConnectionError, Meta = ""
│
│     IsTempFail (4x) or IsPermFail (5x)
│     └── FailureCount++, Status = TemporaryError, Meta = ""
│
│     IsRedirect (3x)
│     └── LastSuccess=visitTime, SuccessCount++, Meta=response.Meta, Status=Redirect
│
│     IsInput (1x)
│     └── LastSuccess=visitTime, SuccessCount++, Meta=response.Meta, Status=Interactive
│
│     else → Status = UNKNOWN, Meta = ""
│
│  3. ApplyUrlComponents(urlRecord)
│     Parses NormalizedUrl to set Scheme, Host, Port, PathAndQuery, FileName
│
│  4. db.SaveChanges()   ← flushes UrlRecord to DB
│
│  5. UpsertDocumentAsync(db, urlRecord, parsedResponse, visitTime, ct)
│     See Section 7.2
│
│  6. UpdateLinksAsync(db, urlRecord, parsedResponse, ct)
│     See Section 7.3
│
│  COMMIT TRANSACTION
```

### 7.2 UpsertDocumentAsync

```
parsedResponse is ITextResponse textResponse AND textResponse.HasIndexableText?
│
├─ NO (image, binary, non-indexable text, redirect, error)
│    ─ if existing document row exists: delete it (+ cascade delete DocumentImage)
│    ─ update UrlRecord: IsTextDocument=false, IsImage=(parsedResponse is ImageResponse)
│    ─ set LastMimeType, LastDetectedMimeType
│    ─ if ImageResponse: set ImageWidth, ImageHeight, ImageType on UrlRecord
│    ─ else: clear ImageWidth/Height/Type on UrlRecord
│    return
│
└─ YES (GemTextResponse or PlainTextResponse with non-empty content)
     ─ look up existing DocumentRecord by NormalizedUrl
     ─ if not found: create new DocumentRecord { UrlRegistryId, NormalizedUrl, CanonicalUrl }
     │
     ─ ResponseHash unchanged?
     │    YES → only update: UrlRegistryId, LastIndexedUtc, StatusCode
     │          (skips FTS trigger — avoids rebuilding FTS for unchanged content)
     │          SaveChanges; return
     │
     └─ ResponseHash changed (or new document)
          Update ALL content fields:
          UrlRegistryId, CanonicalUrl, LastIndexedUtc, StatusCode
          ContentType, MimeType, DetectedMimeType
          IsBodyTruncated, BodySize, BodyHash, ResponseHash
          OutboundLinks, Language, DetectedLanguage=null, LineCount=null
          Title=null, IsFeed=false
          Then from ITextResponse:
            IsSearchable = HasIndexableText
            Content = IndexableText
            Title = Title
            DetectedLanguage = DetectedLanguage
            LineCount = LineCount
            IsFeed = IsFeed
          SaveChanges
          → DocumentsFts triggers fire (Documents_ai or Documents_au)
          → Update UrlRecord image/mime fields as above
```

### 7.3 UpdateLinksAsync

The link graph is rebuilt from scratch on every re-index. The two-dictionary pattern exists to
handle the case where a page links to multiple undiscovered URLs: EF Core will track newly-added
`UrlRecord` entities but they won't appear in a subsequent `db.UrlRegistry.Where(...)` query
until `SaveChanges` is called.

```
UpdateLinksAsync(db, sourceUrl, parsedResponse, ct)
│
│  1. Delete all existing UrlLinks WHERE SourceUrlId = sourceUrl.Id
│
│  2. Build list of distinct FoundLinks from parsedResponse.Links
│     targetUrls = all NormalizedUrls from those links
│
│  3. existingTargets = db.UrlRegistry.Where(u => targetUrls.Contains(u.NormalizedUrl))
│                       .ToDictionary(u => u.NormalizedUrl)
│     (loads URL records that already exist in DB)
│
│  4. trackedTargets = db.ChangeTracker entries for UrlRecord that are not Deleted
│                     .ToDictionary(u => u.NormalizedUrl)
│     (handles newly-added records not yet flushed to DB)
│
│  5. For each foundLink:
│       if NOT in existingTargets AND NOT in trackedTargets:
│           create new UrlRecord(foundLink.Url.NormalizedUrl) { FirstSeen = ... }
│           ApplyUrlComponents(target)
│           db.UrlRegistry.Add(target)
│           trackedTargets[target.NormalizedUrl] = target
│
│  6. SaveChanges  ← flushes new UrlRecord rows, assigns IDs
│
│  7. For each foundLink:
│       resolve target from existingTargets or trackedTargets
│       db.UrlLinks.Add(new UrlLinkRecord {
│           SourceUrlId = sourceUrl.Id,
│           TargetUrlId = target.Id,
│           IsExternal  = foundLink.IsExternal,
│           LinkText    = foundLink.LinkText.Trim()
│       })
│
│  8. SaveChanges  ← inserts all UrlLink rows
```

---

## 8. FTS Index Management

### 8.1 DocumentsFts — trigger-maintained

The FTS5 index on `Documents` is kept in sync automatically by three SQLite triggers. The triggers
are created once by `KennedyDbContext.EnsureFtsAsync()` and run forever after that without any
application code involvement. The key optimisation is in `UpsertDocumentAsync`: if
`ResponseHash` is unchanged, the code only updates `LastIndexedUtc` and does NOT update `Content`
or `Title`, so no trigger fires and the FTS index is untouched.

### 8.2 FilesFts — manually rebuilt

Because link text that describes an image can come from *any page in the entire corpus*, it is
not practical to maintain `FilesFts` incrementally. Instead, `FileSearchFtsRebuilder.RebuildAsync`
runs once after all WARC files have been processed.

**Algorithm:**

```
RebuildAsync(ct)
│
│  Open DbContext; BEGIN TRANSACTION
│
│  1. Load all "candidates": UrlRegistry rows where:
│        LastStatusCode BETWEEN 20 AND 29
│        IsTextDocument = 0
│     → candidateMap: Dictionary<long, CandidateRow> (keyed by UrlRegistry.Id)
│
│  2. DELETE FROM FilesFts   (wipe entire FTS table)
│
│  3. Open raw SqliteConnection (bypassing EF Core for streaming)
│     Prepare parameterized INSERT INTO FilesFts(rowid, UrlRegistryId, SearchText)
│
│  4. Execute streaming query:
│        SELECT links.TargetUrlId, links.LinkText
│        FROM UrlLinks links
│        JOIN UrlRegistry targets ON targets.Id = links.TargetUrlId
│        WHERE links.LinkText <> ''
│          AND targets.LastStatusCode BETWEEN 20 AND 29
│          AND targets.IsTextDocument = 0
│        ORDER BY links.TargetUrlId ASC
│     (ordering by TargetUrlId enables group-by streaming without loading into memory)
│
│  5. Stream results, grouping by TargetUrlId:
│        When TargetUrlId changes: call WriteIndexRowAsync for accumulated buffer
│        Accumulate: currentBuffer.Append(linkText)
│     WriteIndexRowAsync:
│        baseText = BuildBaseTerms(candidate.FileName, candidate.PathAndQuery)
│        merged = "$baseText $linkText"
│        TokenCleaner.Replace(merged, " ").Trim().ToLowerInvariant()
│           (TokenCleaner regex: [\W_]+ → strips non-alphanumeric)
│        INSERT INTO FilesFts (rowid=targetId, UrlRegistryId=targetId, SearchText=merged)
│        Add targetId to indexedTargets set
│
│  6. For each candidate NOT in indexedTargets (no inbound links):
│        baseText = BuildBaseTerms(fileName, pathAndQuery)
│        INSERT INTO FilesFts (rowid, UrlRegistryId, SearchText=baseText)
│
│  COMMIT TRANSACTION
```

`BuildBaseTerms(fileName, pathAndQuery)` concatenates them and runs `TokenCleaner` (the `[\W_]+`
regex strips slashes, dots, underscores, etc.), so `/images/cool-photo.png` becomes
`images cool photo png`.

---

## 9. Archive System (`Kennedy.Archive`)

The archive is **entirely separate from the search database**. It stores raw Gemini response bytes
for historical browsing (the "view cached" and "diff" features). It has its own SQLite database
(`archive.db`) and a directory tree of pack files.

### 9.1 Pack file format

Each pack file is a flat binary file containing concatenated records. There is no index or header
in the pack file itself — the byte offset of each record is stored in the Snapshots table.

**Record wire format:**

```
┌──────────────────────┬─────────────────────────┬──────────────────────────┐
│  Type tag (4 bytes)  │  Length (4 bytes uint32) │  Data (Length bytes)     │
│  ASCII, space-padded │  little-endian           │  raw or gzip-compressed  │
└──────────────────────┴─────────────────────────┴──────────────────────────┘
```

Record type tags:

| Tag | Meaning |
|---|---|
| `DATA` | Raw (uncompressed) response bytes |
| `DATZ` | gzip-compressed response bytes |
| `INFO` | UTF-8 text metadata (informational, not used for response reconstruction) |

**Compression decision** (`PackRecordFactory.MakeOptimalRecord`):
Try gzip with `CompressionLevel.SmallestSize`. If `compressed.Length < data.Length * 0.9`
(at least 10% reduction), use `DATZ`. Otherwise use `DATA`.

### 9.2 Pack file routing (`PackManager`)

Pack files are organised by the first 4 hex characters of the content hash (after stripping the
`hash_type:` prefix). Example: hash `sha-256:ab12ef...` → uses first 4 chars `ab12`.

```
Directory structure:
<archiveRoot>/
    <c0><c1>/
        <c2><c3>/
            <c0><c1><c2><c3>   ← this is the pack file (no extension)

Example for hash sha-256:ab12efcd...:
<archiveRoot>/ab/12/ab12
```

`PackManager.GetPack(dataHash)`:
1. Strip the `hash_type:` prefix (e.g. `sha-256:`)
2. Take first 4 chars of hex string as the pack name
3. Build path: `<root>/<c0><c1>/<c2><c3>/`
4. Return `PackFile(path, packName)` — note the file is named with just the 4 chars, not the full hash

### 9.3 Archiver.ArchiveResponse

```
ArchiveResponse(GeminiResponse response, bool isPublic = true)
│
│  ShouldBeArchived?
│  ├─ Status must be: success+body OR redirect OR input OR auth
│  ├─ Non-text AND BodySize > 5MB → skip
│  └─ Non-text AND truncated → skip
│
│  AlreadyInArchive?
│  └─ Snapshots WHERE UrlId=url.ID AND Captured=response.ResponseReceived → skip
│
│  TruncateIfNecessary: if BodySize > 5MB → truncate to 5MB, set IsBodyTruncated=true
│
│  Ensure Url row exists; create if missing
│
│  respBytes = GeminiParser.CreateResponseBytes(response)   ← reconstruct wire bytes
│  dataHash  = GeminiParser.GetStrongHash(respBytes)        ← "sha-256:hexstring"
│
│  Create Snapshot object (IsDuplicate=false initially)
│
│  previousSnapshots = db.Snapshots WHERE DataHash = dataHash
│  first = previousSnapshots.FirstOrDefault()
│
│  if first == null:
│      packFile = packManager.GetPack(dataHash)
│      snapshot.Offset = packFile.Append(PackRecordFactory.MakeOptimalRecord(respBytes))
│  else:
│      snapshot.Offset = first.Offset                 ← reuse existing data
│      snapshot.IsDuplicate = previousSnapshots WHERE UrlId=snapshot.UrlId .Any()
│      snapshot.IsGlobalDuplicate = previousSnapshots WHERE UrlId≠snapshot.UrlId .Any()
│
│  db.Snapshots.Add(snapshot)
│  db.SaveChanges()
```

### 9.4 SnapshotReader.ReadResponse

```
ReadResponse(Snapshot snapshot)
│
│  pack = packManager.GetPack(snapshot.DataHash)
│  record = pack.Read(snapshot.Offset)    ← seeks to offset, reads 4+4+N bytes
│  bytes = (record.Type == "DATZ") ? GzipUtils.Decompress(record.Data) : record.Data
│  return GeminiParser.ParseResponseBytes(snapshot.Url.GeminiUrl, bytes)
```

---

## 10. Search Layer (`Kennedy.Search`)

### 10.1 UserQuery

`UserQuery` is an immutable record produced by `QueryParser`. It separates the raw input into
structured components:

| Field | Type | Populated by |
|---|---|---|
| `RawQuery` | string | Original user input |
| `TermsQuery` | string | Input with all scope operators removed |
| `FtsQuery` | string? | FTS5-ready query string; null if only scopes (no terms) |
| `SiteScope` | string? | Value from `site:domain` |
| `FileTypeScope` | string? | Value from `filetype:ext` |
| `TitleScope` | string? | Value from `intitle:term` or `intitle:"phrase"` |
| `UrlScope` | string? | Value from `inurl:pattern` |

Computed booleans: `HasFtsQuery`, `HasSiteScope`, `HasFileTypeScope`, `HasTitleScope`, `HasUrlScope`,
`IsValidTextQuery`, `IsValidImageQuery`, `IsSimpleQuery`.

`IsValidTextQuery` — true if **any** of the scope/term fields are set (prevents empty queries).
`IsValidImageQuery` — same but excludes `HasTitleScope` (images have no title field).

### 10.2 QueryParser

```
QueryParser.Parse(inputQuery)
│
│  Normalize: collapse whitespace runs; trim
│
│  Extract (each is regex match; matched text is removed from terms):
│  ├─ TitleScope:    \bintitle:\s*([^"\s]+)\b
│  │                 \bintitle:\s*"([^"]+)"
│  ├─ SiteScope:     \bsite:\s*([0-9a-z\-\.]+)\b   (lowercase domains only)
│  ├─ FileTypeScope: \bfiletype:\s*([0-9a-z\-\.]+)\b
│  └─ UrlScope:      \binurl:\s*"?([^\s"]+)"?
│
│  Remaining → TermsQuery
│  FtsQuery = TermsQuery.IsNullOrWhitespace ? null : FtsSyntaxConverter.Convert(TermsQuery)
```

### 10.3 FtsSyntaxConverter

Converts user query text into valid SQLite FTS5 syntax. The key contract is:

- Bare words get wrapped in double-quotes to make them token-level (not prefix) searches:
  `hello world` → `"hello" "world"`
- User-entered double-quoted phrases pass through as FTS5 phrase queries:
  `"hello world"` → `"hello world"`
- `AND`, `OR`, `NOT` are recognised as FTS5 boolean operators only when they appear between
  terms (not inside quotes). Detection is a state machine that looks for the exact character
  sequence `AND`, `OR`, `NOT` followed by whitespace or `(`.
- Single quotes are doubled: `it's` → `it''s` (FTS5 escaping for single quotes inside tokens).

Example transformations:

| User input | FTS5 output |
|---|---|
| `gemini protocol` | `"gemini" "protocol"` |
| `"gemini protocol"` | `"gemini protocol"` |
| `gemini AND protocol` | `"gemini" AND "protocol"` |
| `site:gemini.circumlunar.space` | (scope stripped; no FtsQuery if no remaining terms) |
| `it's fine` | `"it''s" "fine"` |

### 10.4 SqliteSearchService

`SqliteSearchService` connects directly to the SQLite file via `Microsoft.Data.Sqlite` (not EF
Core) for read-only queries. This keeps search queries lean and avoids EF Core overhead.

```csharp
public SqliteSearchService(string sqlitePath)
{
    _connectionString = $"Data Source={sqlitePath}";
}
```

#### SearchText

```sql
SELECT d.CanonicalUrl, d.Title, d.MimeType, d.DetectedLanguage, d.LineCount,
       d.BodySize, d.IsBodyTruncated,
       CASE
           WHEN @fts_has = 1 THEN snippet(DocumentsFts, 1, '[',']','…', 20)
           ELSE substr(d.Content, 1, 180)
       END AS Snippet
FROM Documents d
{INNER|LEFT} JOIN DocumentsFts ON DocumentsFts.rowid = d.Id
WHERE d.IsSearchable = 1
  [AND DocumentsFts MATCH @fts_query]          -- if HasFtsQuery
  [AND d.Title LIKE '%term%']                  -- if HasTitleScope
  [AND d.CanonicalUrl LIKE 'gemini://host/%']  -- if HasSiteScope
  [AND d.MimeType LIKE '%ext%']                -- if HasFileTypeScope
  [AND d.CanonicalUrl LIKE '%pattern%']        -- if HasUrlScope
ORDER BY d.LastIndexedUtc DESC
LIMIT @limit OFFSET @offset;
```

When `HasFtsQuery` is true: `INNER JOIN` (must match FTS), `snippet()` for highlighted excerpt.
When `HasFtsQuery` is false (scope-only): `LEFT JOIN`, first 180 chars of Content as snippet.

The `snippet()` call uses column index 1 (the `Content` column), surrounding match terms with
`[` and `]`, ellipsis `…`, up to 20 tokens of context.

#### SearchImages

```sql
SELECT u.NormalizedUrl, u.ImageType,
       COALESCE(u.ImageWidth, 0), COALESCE(u.ImageHeight, 0),
       0, 0,
       CASE
           WHEN @fts_has = 1 THEN snippet(FilesFts, 1, '[',']','…', 20)
           ELSE COALESCE(substr(FilesFts.SearchText, 1, 180), '')
       END AS Snippet
FROM UrlRegistry u
{INNER|LEFT} JOIN FilesFts ON FilesFts.rowid = u.Id
WHERE u.IsImage = 1
  [AND FilesFts MATCH @fts_query]
  [AND u.NormalizedUrl LIKE 'gemini://host/%']
  [AND u.LastMimeType LIKE '%ext%']
  [AND u.NormalizedUrl LIKE '%pattern%']
ORDER BY u.LastVisit DESC
LIMIT @limit OFFSET @offset;
```

Note that image search queries `UrlRegistry`, not `Documents`. Image metadata (width, height,
type) lives on `UrlRegistry` directly (not in `DocumentImages`), because images are not indexed
as documents.

---

## 11. WARC Writing (`Kennedy.Warc`)

`GeminiWarcCreator` extends `WarcDotNet.WarcWriter` to write Gemini-specific WARC files. This
is used by the **external crawler**, not by Kennedy's indexer. The indexer only reads WARC files.

### WriteSession(GeminiResponse response)

For each crawled URL, writes three WARC records in sequence:

1. **RequestRecord** — the raw Gemini request bytes (`gemini://host/path\r\n`),
   content type `application/gemini; msgtype=request`. Block digest (SHA-256) is set.
   Custom fields: `WARC-Protocol` (e.g. `tls/1.3`), `WARC-Cipher-Suite`.

2. **ResponseRecord** — the raw Gemini response bytes (status line + body),
   content type `application/gemini; msgtype=response`. Block digest and payload digest
   (SHA-256 of body only) are set. If body was truncated: `Truncated: length`.
   `ConcurrentTo` points to the RequestRecord's ID.

3. **MetadataRecord** (once per authority) — the server's TLS certificate in PEM format,
   content type `application/x-pem-file`. Only written the first time each `host:port`
   authority is seen in the current WARC file. Tracked via `WrittenCertificates` dictionary.

All three records share the same `WarcInfoId` (written once via `WriteWarcInfo`).

### Hash format

Both block digest and payload digest use the format `sha-256:<base64>` as produced by
`GeminiParser.GetStrongHash(bytes)`.

---

## 12. robots.txt Support

`Kennedy.Data/RobotsTxt` implements Gemini's restricted subset of the robots.txt exclusion
standard. The Gemini community only standardises `User-agent` and `Disallow` with prefix
matching. All other directives (`Allow`, `Crawl-Delay`, `Sitemap`, wildcards in the middle of
paths) are logged as warnings and ignored.

### RobotsTxtParser.Parse(content)

```
For each line (after stripping # comments and trimming):
│
├─ "user-agent: <value>"
│    Start new group; accumulate currentUserAgents
│    (consecutive user-agent lines share the same rules)
│
├─ "disallow: <path>"
│    inUserAgent = false (next non-user-agent closes the agent group)
│    Strip trailing * → prefix matching only
│    Mid-path * → warn + ignore
│    Add DenyRule(path) for each currentUserAgent
│
├─ "allow:"      → warn + ignore (not in Gemini standard)
├─ "crawl-delay:"→ warn + ignore
├─ "sitemap:"    → warn + ignore
└─ other         → warn + ignore
```

`RobotsTxtParser.Warnings` collects all warning messages with line numbers.

### DenyRule

```csharp
public class DenyRule
{
    public string Path { get; }        // deny prefix (always starts with "/" if non-empty)
    public bool IsAllowAll { get; }    // true when Path is empty: "Disallow:" = allow all
}
```

A `Disallow: ` (empty value) creates a rule where `IsAllowAll = true`. This is the correct
interpretation: an empty Disallow means allow everything.

### RobotsTxtFile.IsPathAllowed(userAgent, path)

```
1. Check * rules (wildcard agent):
   for each DenyRule in Rules["*"]:
       if IsAllowAll: skip
       if path.StartsWith(rule.Path): return false (denied)

2. If userAgent not in Rules: return true (no specific rules = allowed)

3. Check userAgent-specific rules:
   for each DenyRule in Rules[userAgent]:
       if IsAllowAll: skip
       if path.StartsWith(rule.Path): return false (denied)

4. return true (allowed)
```

Note: specific user-agent rules are checked **after** wildcard rules, but both can deny. There
is no "allow overrides deny" logic — the first matching deny wins.

---

## 13. Indexer CLI

`Indexer/Program.cs` is a minimal .NET console application. Paths and WARC file lists are
**hardcoded** in the source — there is no command-line argument parser for file selection (only
an optional `--smoke-query <terms>` argument).

### Startup sequence

```
Main(args)
│
│  1. Hardcoded config:
│     sqlitePath       = "/Users/.../kennedy2.db"
│     languageConfigDir = "/Users/.../config-files/"
│     warcFiles[]      = ["/path/to/2026-02-25.warc.gz"]
│
│  2. Validate WARC files exist; exit(2) if any missing
│
│  3. Configure DI (Microsoft.Extensions.DependencyInjection):
│     LanguageDetector.ConfigFileDirectory = languageConfigDir
│     services.AddDbContextFactory<KennedyDbContext>(options =>
│         options.UseSqlite("Data Source=..."))
│     services.AddScoped<ResponseStore>()
│     services.AddScoped<FileSearchFtsRebuilder>()
│
│  4. EnsureDatabaseCreatedAsync:
│     await db.Database.EnsureCreatedAsync()    ← creates regular tables
│     await db.EnsureFtsAsync()                 ← creates FTS virtual tables + triggers
│
│  5. For each WARC file:
│     WarcIndexer.IndexFileAsync(warcPath, ct)
│       └─ WarcReader iterates records
│          └─ for each ResponseRecord with TargetUri.Scheme = "gemini":
│               GeminiUrl url = new GeminiUrl(responseRecord.TargetUri)
│               GeminiResponse response = GeminiParser.ParseResponseBytes(url,
│                   responseRecord.ContentBlock)
│               response.RequestSent = responseRecord.Date
│               response.ResponseReceived = responseRecord.Date
│               response.IsBodyTruncated = (responseRecord.Truncated?.Length > 0)
│               await responseStore.StoreResponseAsync(response, ct)
│
│  6. FileSearchFtsRebuilder.RebuildAsync(ct)
│
│  7. Optional --smoke-query <terms>:
│     SELECT d.CanonicalUrl, d.Title
│     FROM DocumentsFts f JOIN Documents d ON d.Id = f.rowid
│     WHERE f MATCH $query LIMIT 5
```

Progress is printed to console every 100 records: filename, record count, elapsed seconds,
records-per-second rate. On WARC parse error (`WarcFormatException`), the rest of that WARC
file is skipped with an error message.

---

## 14. Server (`Kennedy.Server`)

The server is a **Gemini protocol server** built on the `RocketForce` framework. It serves
content over Gemini (TLS port 1965), not HTTP. Configuration is loaded from a JSON settings
file (`appsettings.{ENV}.json`) selected by the `ENV` environment variable or a command-line
argument.

### Settings structure

```json
{
  "Settings": {
    "Host": "kennedy.gemi.dev",
    "Port": 1965,
    "CertificateFile": "/path/to/cert.pem",
    "KeyFile": "/path/to/key.pem",
    "PublicRoot": "/path/to/public/",
    "DataRoot": "/path/to/data/"
  }
}
```

`Settings` derives two computed paths:
- `SearchDbFile` = `DataRoot + "kennedy2.db"` (the search SQLite database)
- `ArchiveStatsFile` = `DataRoot + "archive-stats.json"` (pre-computed stats JSON)

### Route map

| Gemini path | Handler | Description |
|---|---|---|
| `/search` | `SearchController.Search` | Full-text search |
| `/image-search` | `ImageSearchController.Search` | Image search |
| `/lucky` | `SearchController.LuckySearch` | Redirect to first result |
| `/stats` | `SearchController.Stats` | Search statistics |
| `/page-info` | `SearchController.UrlInfo` | Info about a specific URL |
| `/site-search/create` | `SearchController.SiteSearchCreate` | Create site-search link |
| `/site-search/s/` | `SearchController.SiteSearchRun` | Run site-scoped search |
| `/archive/cached` | `ArchiveController.Cached` | View cached version of URL |
| `/archive/history` | `ArchiveController.UrlHistory` | Unique capture history |
| `/archive/history-all` | `ArchiveController.UrlFullHistory` | All captures |
| `/archive/diff` | `ArchiveController.Diff` | Diff between two captures |
| `/archive/diff-history` | `ArchiveController.DiffHistory` | Diff history list |
| `/archive/search` | `ArchiveController.Search` | Search archive |
| `/archive/stats` | `ArchiveController.Stats` | Archive statistics |
| `/certs/validator/check` | `CertsController.Check` | TLS cert checker |
| `/reports/domain-backlinks` | `ReportsController.DomainBacklinks` | Backlink report |
| `/reports/site-health` | `ReportsController.SiteHealth` | Site health report |
| `/tools/robots-tester` | `ToolsController.RobotsTester` | robots.txt tester |
| `/tools/url-tester` | `ToolsController.UrlTester` | URL normalisation tester |
| `/observatory/known-hosts` | `SearchController.KnownHosts` | Known Gemini hosts |
| `/observatory/security.txt` | `SearchController.SecurityTxt` | security.txt viewer |

The Server project also integrates with a Wikipedia API client (`Gemipedia`) to surface Wikipedia
article summaries as part of search results. This is an enhancement layer, not core search.

---

## 15. Key Implementation Notes and Gotchas

### 15.1 EF Core + SQLite patterns

**Always use `IDbContextFactory<KennedyDbContext>`** instead of injecting `KennedyDbContext`
directly. Each call to `StoreResponseAsync` creates and disposes a context via the factory.
This avoids SQLite connection pool exhaustion and EF Core's change-tracker growing unbounded
across thousands of WARC records.

```csharp
// DI registration (Indexer/Program.cs)
services.AddDbContextFactory<KennedyDbContext>(options =>
    options.UseSqlite($"Data Source={sqlitePath}"));

// Usage in ResponseStore
await using var db = await _dbFactory.CreateDbContextAsync(ct);
await using var tx = await db.Database.BeginTransactionAsync(ct);
// ... do work ...
await tx.CommitAsync(ct);
```

### 15.2 FTS5 virtual tables and EF Core

EF Core's `EnsureCreated()` does **not** create FTS5 virtual tables or triggers. They must be
created with raw SQL. The `EnsureFtsAsync()` method uses `IF NOT EXISTS` so it is idempotent:

```csharp
await db.EnsureFtsAsync(cancellationToken);
```

Call this **every time** the application starts. It is safe to call on an existing database.

If you use EF Core migrations instead of `EnsureCreated`, add the FTS table and trigger creation
as raw SQL in a `migrationBuilder.Sql(...)` call, or call `EnsureFtsAsync` in a seeding method.

### 15.3 ResponseHash deduplication prevents FTS churn

The `DocumentRecord.ResponseHash` field stores a hash of the **entire Gemini response** (header
+ body). Before writing any content fields to the database, `UpsertDocumentAsync` checks:

```csharp
if (existing.ResponseHash == parsedResponse.Hash)
{
    // Only update mutable timeline fields
    existing.LastIndexedUtc = indexedUtc;
    await db.SaveChangesAsync(ct);
    return;  // ← FTS triggers do NOT fire
}
```

This is critical for performance: re-indexing the same WARC file (e.g. in a rerun) will not
trigger a cascade of FTS index updates for unchanged content. The FTS5 content table approach
also means FTS is the single source of truth for indexed text — the `Documents` table is the
backing store.

### 15.4 ArchiveDbContext uses non-factory pattern

Unlike `KennedyDbContext`, the `ArchiveDbContext` **accepts a path string directly** and
configures SQLite in `OnConfiguring`. The `Archiver` class creates contexts with `GetContext()`
which calls `new ArchiveDbContext(path)` directly. This is an older pattern and is not used in
new code. When implementing, you can refactor to use the factory pattern for consistency.

### 15.5 UrlRecord.Id seeding in Archive.Url

`Archive.Db.Url.Id` is seeded from `GeminiUrl.ID` (a deterministic hash of the URL), not from
SQLite AUTOINCREMENT. This means two different `Archiver` instances pointing at the same
`archive.db` will produce the same `Url.Id` for the same URL. The primary key is explicitly set
in the `Url(GeminiUrl)` constructor:

```csharp
public Url(GeminiUrl url)
{
    Id = url.ID;    // ← deterministic hash, not auto-increment
    ...
}
```

### 15.6 Two-dictionary link insertion pattern

`UpdateLinksAsync` uses two dictionaries to avoid EF Core "duplicate key" exceptions when a
single page links to multiple URLs that don't yet exist in `UrlRegistry`:

- `existingTargets` — loaded from the DB before any adds
- `trackedTargets` — EF Core change tracker (contains newly-added entities)

If you only use the DB query, the second URL from the same source page will fail because the
first URL was added to EF but not yet `SaveChanges`'d. Both dictionaries must be checked.

### 15.7 FilesFts rebuild is intentionally non-incremental

It would be complex to incrementally maintain `FilesFts` because the same image URL can be
linked from hundreds of source pages. A rebuild from scratch is simpler and only runs once
after all WARC files have been processed for an ingestion run, so the performance is acceptable.

### 15.8 NTextCat language detection requires a profile file on disk

`LanguageDetector` loads `Core14.profile.xml` at construction time:
```csharp
langClassifier = factory.Load(ConfigFileDirectory + "Core14.profile.xml");
```

`LanguageDetector.ConfigFileDirectory` is a static property that must be set before any
`LanguageDetector` instance is created. In the Indexer, this is done before DI container
construction. If you forget to set it, the constructor will throw a file-not-found exception.

The profile is part of the NTextCat NuGet package distribution. You can find it in the package
cache or in the `config-files/` directory of this repository.

### 15.9 Non-text file detection order matters

`ResponseParser` tries binary detection **before** text detection:

1. `BinaryParser` uses magic bytes (FileSignatures library). If it identifies the content,
   the result is returned immediately. Text files will NOT match binary magic bytes, so they
   fall through correctly.
2. `TextParser` uses `MimeSniffer.IsText` (WHATWG spec, checks for binary bytes in the first
   1445 bytes). This can incorrectly classify some binary files as text if they happen to not
   contain binary bytes in the header. The ordering ensures known binary types are handled first.

### 15.10 PlainTextResponse suppresses proactive URLs

`/robots.txt`, `/favicon.txt`, and `/.well-known/security.txt` are suppressed from the FTS
index via `UrlUtility.IsProactiveUrl`. These are URLs the crawler fetches proactively for
every host, not because a link was found. Their content (a list of disallow rules, an icon,
or a security contact) should not pollute search results.

`PlainTextResponse.HasIndexableText` returns false for proactive URLs:

```csharp
public bool HasIndexableText => !IsProactiveRequest && (BodyText.Length > 0);
```

`IsProactiveRequest` is defined on `ParsedResponse` and delegates to `UrlUtility.IsProactiveUrl`.

### 15.11 FtsSyntaxConverter is a character-by-character state machine

The converter is not a regex — it is a state machine that tracks whether the current position is
inside an explicit quote, an implicit quote (wrapping a bare word), or unquoted. The "AND/OR/NOT"
detection works by recognising the initial character `A`, `O`, or `N` and then checking subsequent
characters one by one. If the full keyword is not confirmed (e.g. `Andrew` starts with `A` + `n`
but then `d` breaks the `AND` sequence), the accumulated characters are output as part of an
implicit quote.

Do not try to replace this with regex-based substitution — the interaction between quoted
phrases, operators, and bare words requires the state machine approach.

### 15.12 SQLite write serialisation

SQLite in WAL mode can handle one writer and many readers concurrently. The `Indexer` is the
only writer to `kennedy2.db` during ingestion; the `Server` is read-only. If you run multiple
`Indexer` processes against the same database simultaneously, you will get `SQLITE_BUSY` errors.
The current design assumes single-writer ingestion.

### 15.13 Documents.UrlRegistryId nullable FK

`UrlRegistryId` on `DocumentRecord` is declared nullable (`long?`) and configured with
`OnDelete(DeleteBehavior.SetNull)`. This means if a `UrlRecord` is deleted, the corresponding
`DocumentRecord` survives with `UrlRegistryId = NULL`. In practice `UrlRecord` rows are never
deleted during normal operation, but the nullable FK prevents orphan document errors.

---

## 16. Step-by-Step Implementation Checklist

Use this checklist to implement Kennedy 2.0 from scratch. Steps are roughly in dependency order.

### Phase 1: Core infrastructure

- [ ] **1.1** Clone `Gemini.Net` and `Warc.Net` as sibling repositories. Verify you can build them.
- [ ] **1.2** Create solution `Kennedy.sln` with projects: `Kennedy.Data`, `Kennedy.Search`,
  `Kennedy.Archive`, `Kennedy.Warc`, `Kennedy.Indexer` (console app), `Kennedy.Server` (console app).
- [ ] **1.3** Add project references per the dependency graph in Section 3. Add NuGet references:
  - `Kennedy.Data`: EF Core Sqlite 9.x, SixLabors.ImageSharp 3.x, FileSignatures 5.x, NTextCat 0.3.65
  - `Kennedy.Search`: EF Core Sqlite 9.x
  - `Kennedy.Server`: RocketForce, Microsoft.Extensions.Configuration.Json, Newtonsoft.Json, DiffPlex

### Phase 2: Domain models and database

- [ ] **2.1** Create `Kennedy.Data/Models/UrlStatus.cs` enum with all 13 values (Section 5.1).
- [ ] **2.2** Create `Kennedy.Data/Models/ContentType.cs` enum (5 values).
- [ ] **2.3** Create `Kennedy.Data/Models/UrlRecord.cs` with all properties, `[Table]`, `[Index]`
  attributes, and both constructors (default + `(string normalizedUrl)`).
- [ ] **2.4** Create `Kennedy.Data/Models/DocumentRecord.cs` with all properties, `[Table]`, `[Index]`
  attributes, and nullable `Image` navigation property.
- [ ] **2.5** Create `Kennedy.Data/Models/DocumentImageRecord.cs` as shared-PK 1:1 with `DocumentRecord`.
- [ ] **2.6** Create `Kennedy.Data/Models/UrlLinkRecord.cs` with both FK indexes.
- [ ] **2.7** Create `Kennedy.Data/KennedyDbContext.cs`:
  - `DbSet<UrlRecord> UrlRegistry`
  - `DbSet<DocumentRecord> Documents`
  - `DbSet<DocumentImageRecord> DocumentImages`
  - `DbSet<UrlLinkRecord> UrlLinks`
  - `OnModelCreating`: configure `Document→UrlRegistry` SetNull, `Document→Image` Cascade
  - `EnsureFtsAsync(CancellationToken)`: create DocumentsFts virtual table + 3 triggers + FilesFts
    using `Database.ExecuteSqlRawAsync` with `CREATE VIRTUAL TABLE IF NOT EXISTS` / `CREATE TRIGGER IF NOT EXISTS`

### Phase 3: Parsing pipeline

- [ ] **3.1** Create `Kennedy.Data/ContentType.cs` (if not done).
- [ ] **3.2** Create `Kennedy.Data/FoundLink.cs`: `Url`, `IsExternal`, `LinkText` properties;
  `IEquatable<FoundLink>` based on `Url`; static `Create(pageUrl, foundUrl, linkText)`.
- [ ] **3.3** Create `Kennedy.Data/ITextResponse.cs` interface with the 6 members.
- [ ] **3.4** Create `Kennedy.Data/ParsedResponse.cs` inheriting `GeminiResponse`:
  - `FormatType`, `DetectedMimeType`, `Links`
  - `IsProactiveRequest` property
  - Constructor that copies all fields from a `GeminiResponse`
- [ ] **3.5** Create `Kennedy.Data/ImageResponse.cs` inheriting `ParsedResponse`:
  - `Width`, `Height`, `ImageType`, `IsTransparent` (required init properties)
- [ ] **3.6** Create `Kennedy.Data/GemTextResponse.cs` inheriting `ParsedResponse`, implementing
  `ITextResponse`: `DetectedLanguage`, `HasIndexableText`, `IndexableText`, `IsFeed`, `LineCount`,
  `Title`, `Mentions`, `HashTags`.
- [ ] **3.7** Create `Kennedy.Data/PlainTextResponse.cs` inheriting `ParsedResponse`, implementing
  `ITextResponse`: `HasIndexableText = !IsProactiveRequest && BodyText.Length > 0`, lazy `LineCount`.
- [ ] **3.8** Create `Kennedy.Data/Utils/UrlUtility.cs` with `IsProactiveUrl`, `IsFaviconUrl`,
  `IsRobotsUrl`, `IsSecurityUrl`.
- [ ] **3.9** Create `Kennedy.Data/Utils/Bag.cs` generic frequency bag.
- [ ] **3.10** Create `Kennedy.Data/Parsers/GemText/LineParser.cs`: `GetLines` (split on `\n`),
  `RemovePreformattedLines` (toggle `inPre` on ` ``` ` lines), `IsHeading`, `ParseHeading`.
- [ ] **3.11** Create `Kennedy.Data/Parsers/GemText/LinkFinder.cs`: static `GetLinks` (regex
  `^=>\s*([^\s]+)\s*(.*)`; skip non-gemini absolute URLs; call `FoundLink.Create`), `GetLinkText`.
- [ ] **3.12** Create `Kennedy.Data/Parsers/GemText/TitleFinder.cs`: `FindTitle` — first heading,
  fallback to first preformatted alt text (text after opening ` ``` `).
- [ ] **3.13** Create `Kennedy.Data/Parsers/GemText/HashtagsFinder.cs`: regex `[\,\s]#([a-zA-Z0-9][a-zA-Z0-9_\-]+)`;
  exclude all-numeric, 3-char hex, 6-char hex.
- [ ] **3.14** Create `Kennedy.Data/Parsers/GemText/MentionsFinder.cs`: two regexes for `@`/`~`
  usernames; filter lines (link lines: use link text only; heading lines: heading text only).
- [ ] **3.15** Create `Kennedy.Data/Parsers/LanguageDetector.cs`: static `ConfigFileDirectory`;
  load `Core14.profile.xml` via `RankedLanguageIdentifierFactory`; min 150 / max 4096 chars;
  return ISO 639-1 two-letter code via `CultureInfo`.
- [ ] **3.16** Create `Kennedy.Data/Parsers/MimeSniffer.cs`: `IsText(byte[])` — check BOM bytes
  first (UTF-16, UTF-8), then scan first 1445 bytes for binary bytes per WHATWG spec.
- [ ] **3.17** Create `Kennedy.Data/Parsers/AbstractTextParser.cs`: abstract `CanParse` + `Parse`.
- [ ] **3.18** Create `Kennedy.Data/Parsers/GemTextResponseParser.cs` extending `AbstractTextParser`:
  `CanParse`: `isTextBody && MimeType.StartsWith("text/gemini")`. `Parse`: run all GemText
  sub-parsers, build `GemTextResponse`. `IsGemFeed`: regex `^\d{4}\-[01]\d\-[0123]\d`, ≥2 matches.
- [ ] **3.19** Create `Kennedy.Data/Parsers/PlainTextResponseParser.cs`: `CanParse`: `isTextBody &&
  MimeType == "text/plain"`.
- [ ] **3.20** Create `Kennedy.Data/Parsers/BinaryParser.cs`: `FileFormatInspector.DetermineFileFormat`;
  if image → `Image.Identify`; else generic binary.
- [ ] **3.21** Create `Kennedy.Data/Parsers/TextParser.cs`: `MimeSniffer` + ordered list of
  `AbstractTextParser` implementations.
- [ ] **3.22** Create `Kennedy.Data/Parsers/ResponseParser.cs`: orchestrates `TryParseRedirect` →
  `BinaryParser` → `TextParser` → fallback binary.

### Phase 4: Response storage

- [ ] **4.1** Create `Kennedy.Data/Services/ResponseStore.cs`:
  - Constructor takes `IDbContextFactory<KennedyDbContext>`
  - `StoreResponseAsync`: parse → open db → begin tx → upsert URL → upsert document → update links → commit
  - `ApplyUrlLifecycle`: status machine per Gemini status code groups
  - `ApplyUrlComponents`: parse `Uri` to fill `Scheme/Host/Port/PathAndQuery/FileName`
  - `UpsertDocumentAsync`: handle non-indexable (delete document) vs indexable (upsert + ResponseHash skip)
  - `UpdateLinksAsync`: delete old links + two-dictionary new-URL tracking pattern

### Phase 5: FTS rebuilder

- [ ] **5.1** Create `Kennedy.Data/Services/FileSearchFtsRebuilder.cs`:
  - Load all non-text candidate URLs
  - Delete FilesFts
  - Open raw `SqliteConnection` for streaming insert
  - Stream `UrlLinks JOIN UrlRegistry` ordered by `TargetUrlId`
  - Group by TargetUrlId, concatenate link texts + BuildBaseTerms
  - Insert remaining candidates with no inbound links

### Phase 6: Archive system

- [ ] **6.1** Create `Kennedy.Archive/Pack/PackRecord.cs`: `Type` (string), `Data` (byte[]).
- [ ] **6.2** Create `Kennedy.Archive/GzipUtils.cs`: `Compress` / `Decompress` using `GZipStream`.
- [ ] **6.3** Create `Kennedy.Archive/Pack/PackRecordFactory.cs`: `ToBytes` (4-byte ASCII type tag +
  4-byte uint32 length + data), `MakeOptimalRecord` (gzip if < 90%), `MakeDataRecord`, `MakeDatzRecord`.
- [ ] **6.4** Create `Kennedy.Archive/Pack/PackFile.cs`: `Append` (get current file size as offset,
  append bytes), `Read` (seek to offset, read 4+4+N bytes, parse type string).
- [ ] **6.5** Create `Kennedy.Archive/Pack/PackManager.cs`: `GetPack(dataHash)` — strip `hash_type:`,
  take first 4 chars, build 2-level directory path, return `PackFile`.
- [ ] **6.6** Create `Kennedy.Archive/Db/Url.cs` with `Id` seeded from `GeminiUrl.ID`, `[Table("Urls")]`,
  `GeminiUrl` computed property.
- [ ] **6.7** Create `Kennedy.Archive/Db/Snapshot.cs` with all fields, `[Table("Snapshots")]`, three indexes.
- [ ] **6.8** Create `Kennedy.Archive/Db/ArchiveDbContext.cs`: `OnConfiguring` for SQLite path;
  `HasMany`/`WithOne` relationship between `Url` and `Snapshot`.
- [ ] **6.9** Create `Kennedy.Archive/SnapshotReader.cs`: `ReadResponse(Snapshot)` — get pack, read
  record at offset, decompress if DATZ, parse with `GeminiParser.ParseResponseBytes`.
- [ ] **6.10** Create `Kennedy.Archive/Archiver.cs`:
  - `ShouldBeArchived`: status + size/truncation filters
  - `AlreadyInArchive`: check by `UrlId + Captured` timestamp
  - `TruncateIfNecessary`: cap at 5MB
  - `ArchiveResponse`: full dedup + pack append + snapshot insert logic
  - `GetLatestResponse`, `GetArchiveStats` helper methods
- [ ] **6.11** Create `Kennedy.Archive/ArchiveStats.cs`.

### Phase 7: WARC writing (for crawler integration)

- [ ] **7.1** Create `Kennedy.Warc/GeminiWarcCreator.cs` extending `WarcDotNet.WarcWriter`:
  - `WriteSession`: request record, response record (with payload digest, block digest, TLS fields),
    certificate metadata record (once per authority)
  - Custom WARC fields: `WARC-Protocol`, `WARC-Cipher-Suite`
  - `WrittenCertificates` dictionary keyed by authority string
  - `WriteLegacySession` and `WriteLegacyCertificate` for importing historical data

### Phase 8: Search layer

- [ ] **8.1** Create `Kennedy.Search/Models/UserQuery.cs`: all fields + computed booleans.
- [ ] **8.2** Create `Kennedy.Search/Models/TextSearchResult.cs` and `ImageSearchResult.cs`.
- [ ] **8.3** Create `Kennedy.Search/Query/FtsSyntaxConverter.cs`: character-by-character state
  machine for bare-word quoting, AND/OR/NOT detection, single-quote escaping. Test thoroughly
  with the example transformations in Section 10.3.
- [ ] **8.4** Create `Kennedy.Search/Query/QueryParser.cs`: normalize whitespace; extract and remove
  scope operators; build `FtsQuery` via converter.
- [ ] **8.5** Create `Kennedy.Search/Services/ISearchService.cs` interface (4 methods).
- [ ] **8.6** Create `Kennedy.Search/Services/SqliteSearchService.cs`:
  - Constructor takes SQLite file path string
  - `SearchText`: INNER/LEFT JOIN on `DocumentsFts`, `snippet()` vs substr, `BuildTextFilters`
  - `SearchImages`: INNER/LEFT JOIN on `FilesFts`, query `UrlRegistry` (not Documents)
  - `BuildTextFilters`, `BuildImageFilters`, `AppendCommonDocumentFilters`, `AppendCommonUrlFilters`

### Phase 9: robots.txt

- [ ] **9.1** Create `Kennedy.Data/RobotsTxt/DenyRule.cs`: path storage, `IsAllowAll` when empty,
  auto-prepend `/` if missing, strip trailing `*`.
- [ ] **9.2** Create `Kennedy.Data/RobotsTxt/RobotsTxtFile.cs`: `Rules` dictionary; `AddDenyRule`;
  `IsPathAllowed` (wildcard first, then specific agent, prefix matching).
- [ ] **9.3** Create `Kennedy.Data/RobotsTxt/RobotsTxtParser.cs`: line-by-line parser;
  user-agent group accumulation; directives; warning collection; `CreateRobotsUrl` helper.

### Phase 10: Indexer CLI

- [ ] **10.1** Create `Indexer/WarcIndexer.cs`: iterates `WarcReader`, filters `ResponseRecord`,
  extracts `GeminiUrl` + `GeminiResponse`, calls `ResponseStore.StoreResponseAsync`.
- [ ] **10.2** Create `Indexer/Program.cs`:
  - Hardcode (or argument-parse) SQLite path + language config dir + WARC file list
  - Configure DI (`AddDbContextFactory`, `AddScoped<ResponseStore>`, `AddScoped<FileSearchFtsRebuilder>`)
  - `EnsureDatabaseCreatedAsync`: both `EnsureCreated()` and `EnsureFtsAsync()`
  - Process all WARCs; rebuild FilesFts; optional smoke query

### Phase 11: Server (optional — serves queries over Gemini)

- [ ] **11.1** Create `Kennedy.Server/Settings.cs` with required configuration properties.
- [ ] **11.2** Create `Kennedy.Server/RoutePaths.cs` with all route constants and URL-building helpers.
- [ ] **11.3** Create controllers (`SearchController`, `ImageSearchController`, `ArchiveController`,
  `CertsController`, `ReportsController`, `ToolsController`).
- [ ] **11.4** Create view classes in `Views/Search/`, `Views/Archive/`, `Views/Reports/`, `Views/Certs/`,
  `Views/Tools/`. Views render Gemtext responses using `RocketForce` response helpers.
- [ ] **11.5** Create `Server/Program.cs`: load settings, load TLS certificate, instantiate
  `GeminiServer`, register all routes, call `server.Run()`.

### Phase 12: Integration testing

- [ ] **12.1** Obtain a WARC file containing Gemini responses (e.g. from a test crawler run or
  by using `GeminiWarcCreator` to create a synthetic one).
- [ ] **12.2** Run the Indexer. Verify `kennedy2.db` is created with rows in all tables.
- [ ] **12.3** Run the smoke query: `--smoke-query "gemini"`. Verify results are returned.
- [ ] **12.4** Check the `FilesFts` table: `SELECT COUNT(*) FROM FilesFts;` should be > 0 if any
  image URLs were in the WARC.
- [ ] **12.5** Run the Server and issue a Gemini request to `gemini://localhost:1965/search?gemini`.
  Verify search results are returned in Gemtext format.
- [ ] **12.6** Test the archive: confirm a `Snapshot` row was inserted and the pack file exists on
  disk at the expected path.
- [ ] **12.7** Test the `FtsSyntaxConverter` with edge cases: quoted phrases, AND/OR/NOT operators,
  single quotes, mixed queries.

---

*This document was generated from source analysis of the Kennedy 2.0 codebase as of commit `f56a850`
(branch `kennedy2`). All code paths have been verified against the actual source files.*
