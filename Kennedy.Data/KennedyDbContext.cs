using Kennedy.Data.Models;
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
        public DbSet<DocumentImageRecord> DocumentImages => Set<DocumentImageRecord>();

        /// <summary>Directed link graph: source URL → target URL.</summary>
        public DbSet<UrlLinkRecord> UrlLinks => Set<UrlLinkRecord>();

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

            modelBuilder.Entity<DocumentRecord>()
                .HasOne(d => d.Image)
                .WithOne(i => i.Document)
                .HasForeignKey<DocumentImageRecord>(i => i.DocumentId)
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

        public async Task EnsureFtsAsync(CancellationToken ct)
        {
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

            // UrlIndex enables fast inurl substring search. The trigram tokenizer indexes every
            // 3-char sequence so arbitrary substrings (including '/', '.', ':') can be matched
            // without a leading-wildcard LIKE scan.
            await Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS UrlIndex USING fts5(
                    Url,
                    tokenize='trigram'
                );
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS UrlRegistry_ai AFTER INSERT ON UrlRegistry BEGIN
                    INSERT INTO UrlIndex(rowid, Url) VALUES (new.Id, new.NormalizedUrl);
                END;
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS UrlRegistry_ad AFTER DELETE ON UrlRegistry BEGIN
                    INSERT INTO UrlIndex(UrlIndex, rowid, Url) VALUES ('delete', old.Id, old.NormalizedUrl);
                END;
                """,
                ct);

            // Backfill rows that existed before the trigger was installed.
            await Database.ExecuteSqlRawAsync(
                """
                INSERT INTO UrlIndex(rowid, Url)
                SELECT Id, NormalizedUrl FROM UrlRegistry
                WHERE Id NOT IN (SELECT rowid FROM UrlIndex);
                """,
                ct);
        }

    }
}
