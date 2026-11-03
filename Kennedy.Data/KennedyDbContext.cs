using Kennedy.Data.Models;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data
{
    /// <summary>
    /// EF Core DbContext for the Kennedy search database.
    /// Contains the URL registry, indexed documents, image metadata, and link graph.
    /// FTS5 virtual tables (DocumentsFts, FilesFts) and their triggers are NOT managed by
    /// EF migrations — call <see cref="EnsureFtsAsync"/> once after <c>EnsureCreated</c>.
    /// </summary>
    public class KennedyDbContext : DbContext
    {
        /// <summary>Registry of every URL the crawler has ever seen.</summary>
        public DbSet<UrlRecord> UrlRegistry => Set<UrlRecord>();

        /// <summary>Current searchable document representation for each URL.</summary>
        public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();

        /// <summary>Image dimension/type metadata for image URLs.</summary>
        public DbSet<DocumentImageRecord> Images => Set<DocumentImageRecord>();

        /// <summary>Directed link graph: source URL → target URL.</summary>
        public DbSet<UrlLinkRecord> UrlLinks => Set<UrlLinkRecord>();

        /// <summary>Validated emoji favicons, one per capsule authority.</summary>
        public DbSet<FaviconRecord> Favicons => Set<FaviconRecord>();

        public DbSet<RobotsTxtRecord> RobotsTxts => Set<RobotsTxtRecord>();
        public DbSet<SecurityTxtRecord> SecurityTxts => Set<SecurityTxtRecord>();
        public DbSet<IndexWorkItem> IndexWorkItems => Set<IndexWorkItem>();

        public KennedyDbContext(DbContextOptions<KennedyDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Configures relationships not inferable from conventions:
        /// Document→UrlRegistry (nullable FK, SetNull on delete) and
        /// Document→DocumentImage (one-to-one, cascade delete).
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DocumentRecord>()
                .HasOne<UrlRecord>()
                .WithMany()
                .HasForeignKey(d => d.UrlRegistryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DocumentImageRecord>()
                .HasOne(i => i.Url)
                .WithOne()
                .HasForeignKey<DocumentImageRecord>(i => i.UrlRegistryId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        /// <summary>
        /// Creates the FTS5 virtual tables if they don't already exist.
        /// Must be called manually after <c>Database.EnsureCreated()</c> because EF Core cannot manage virtual tables.
        /// <list type="bullet">
        ///   <item><term>DocumentsFts</term><description>Standalone full-text index over Documents (Title, Content, CanonicalUrl); managed explicitly by <see cref="Kennedy.Data.Services.ResponseStore"/>.</description></item>
        ///   <item><term>FilesFts</term><description>Index for non-text file search; rebuilt externally by <see cref="Kennedy.Data.Services.FileSearchFtsRebuilder"/>.</description></item>
        /// </list>
        /// </summary>
        /// <summary>
        /// Sets SQLite pragmas that significantly improve bulk-write performance.
        /// Call once after opening a connection that will be used for large imports.
        /// <list type="bullet">
        ///   <item><term>journal_mode=WAL</term><description>Allows concurrent readers during writes; reduces fsync pressure.</description></item>
        ///   <item><term>synchronous=NORMAL</term><description>Skips per-commit fsync while remaining crash-safe at the WAL checkpoint level.</description></item>
        ///   <item><term>cache_size=-65536</term><description>64 MB page cache; reduces read I/O on repeated lookups.</description></item>
        ///   <item><term>temp_store=MEMORY</term><description>Keeps SQLite temporary tables in RAM.</description></item>
        ///   <item><term>mmap_size=268435456</term><description>256 MB memory-mapped I/O window for faster sequential reads.</description></item>
        /// </list>
        /// </summary>
        public async Task ApplyPerformancePragmasAsync(CancellationToken ct = default)
        {
            await Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", ct);
            await Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", ct);
            await Database.ExecuteSqlRawAsync("PRAGMA cache_size = -65536;", ct);
            await Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;", ct);
            await Database.ExecuteSqlRawAsync("PRAGMA mmap_size = 268435456;", ct);
        }

        public async Task EnsureSchemaCompatibilityAsync(CancellationToken ct)
        {
            await Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS Favicons (
                    Id INTEGER NOT NULL CONSTRAINT PK_Favicons PRIMARY KEY AUTOINCREMENT,
                    Scheme TEXT NOT NULL,
                    Host TEXT NOT NULL,
                    Port INTEGER NOT NULL,
                    Emoji TEXT NOT NULL,
                    SourceUrlRegistryId INTEGER NOT NULL
                );
                """, ct);
            await Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_Favicons_Scheme_Host_Port ON Favicons (Scheme, Host, Port);", ct);
            await Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS RobotsTxts (
                    Id INTEGER NOT NULL CONSTRAINT PK_RobotsTxts PRIMARY KEY AUTOINCREMENT,
                    Scheme TEXT NOT NULL,
                    Host TEXT NOT NULL,
                    Port INTEGER NOT NULL,
                    Content TEXT NOT NULL,
                    SourceUrlRegistryId INTEGER NOT NULL
                );
                """, ct);
            await Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_RobotsTxts_Scheme_Host_Port ON RobotsTxts (Scheme, Host, Port);", ct);
            await Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS SecurityTxts (
                    Id INTEGER NOT NULL CONSTRAINT PK_SecurityTxts PRIMARY KEY AUTOINCREMENT,
                    Scheme TEXT NOT NULL,
                    Host TEXT NOT NULL,
                    Port INTEGER NOT NULL,
                    Content TEXT NOT NULL,
                    SourceUrlRegistryId INTEGER NOT NULL
                );
                """, ct);
            await Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_SecurityTxts_Scheme_Host_Port ON SecurityTxts (Scheme, Host, Port);", ct);
            await Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS IndexWorkItems (
                    UrlRegistryId INTEGER NOT NULL CONSTRAINT PK_IndexWorkItems PRIMARY KEY,
                    WorkTypes INTEGER NOT NULL
                );
                """, ct);

            var connection = Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Images);";

            var hasImagesTable = false;
            var hasLastIndexedUtc = false;
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    hasImagesTable = true;
                    if (string.Equals(reader.GetString(1), "LastIndexedUtc", StringComparison.OrdinalIgnoreCase))
                    {
                        hasLastIndexedUtc = true;
                    }
                }
            }

            if (hasImagesTable && !hasLastIndexedUtc)
            {
                await Database.ExecuteSqlRawAsync(
                    "ALTER TABLE Images ADD COLUMN LastIndexedUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';",
                    ct);
            }
        }

        public async Task EnsureFtsAsync(CancellationToken ct)
        {
            // Replace the old contentless-table deletion trigger: UrlSearch is a
            // standalone FTS table and requires an ordinary DELETE.
            await Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS UrlRegistry_ad;", ct);
            // Keep FTS schema setup explicit because EnsureCreated does not create virtual tables.
            // Standalone (not content=) so FTS rows are self-contained; ResponseStore manages inserts/deletes.
            // Both tables use porter unicode61 tokenization so that "cats" matches "cat" (stemming parity
            // with old Kennedy, which also used porter on its FTS and ImageSearch tables).

            await Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS DocumentsFts USING fts5(
                    Title,
                    Content,
                    CanonicalUrl,
                    tokenize='porter unicode61'
                );
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS FilesFts USING fts5(
                    UrlRegistryId UNINDEXED,
                    SearchText,
                    tokenize='porter unicode61'
                );
                """,
                ct);

            // UrlSearch enables fast inurl substring search over URL path+query only.
            // The trigram tokenizer indexes 3-char sequences so patterns like "/cgi-bin/"
            // can be matched efficiently.
            await Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS UrlSearch USING fts5(
                    PathAndQuery,
                    tokenize='trigram'
                );
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS UrlRegistry_ai AFTER INSERT ON UrlRegistry BEGIN
                    INSERT INTO UrlSearch(rowid, PathAndQuery) VALUES (
                        new.Id,
                        CASE
                            WHEN instr(new.NormalizedUrl, '://') = 0 THEN new.NormalizedUrl
                            WHEN instr(substr(new.NormalizedUrl, instr(new.NormalizedUrl, '://') + 3), '/') = 0 THEN '/'
                            ELSE substr(
                                new.NormalizedUrl,
                                instr(new.NormalizedUrl, '://') + 2 + instr(substr(new.NormalizedUrl, instr(new.NormalizedUrl, '://') + 3), '/')
                            )
                        END
                    );
                END;
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS UrlRegistry_ad AFTER DELETE ON UrlRegistry BEGIN
                    DELETE FROM UrlSearch WHERE rowid=old.Id;
                END;
                """,
                ct);

            // Backfill rows that existed before the trigger was installed.
            await Database.ExecuteSqlRawAsync(
                """
                INSERT INTO UrlSearch(rowid, PathAndQuery)
                SELECT
                    Id,
                    CASE
                        WHEN instr(NormalizedUrl, '://') = 0 THEN NormalizedUrl
                        WHEN instr(substr(NormalizedUrl, instr(NormalizedUrl, '://') + 3), '/') = 0 THEN '/'
                        ELSE substr(
                            NormalizedUrl,
                            instr(NormalizedUrl, '://') + 2 + instr(substr(NormalizedUrl, instr(NormalizedUrl, '://') + 3), '/')
                        )
                    END
                FROM UrlRegistry
                WHERE Id NOT IN (SELECT rowid FROM UrlSearch);
                """,
                ct);
        }

    }
}
