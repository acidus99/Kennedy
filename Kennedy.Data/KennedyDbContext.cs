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
        /// Creates the FTS5 virtual tables and their synchronization triggers if they don't already exist.
        /// Must be called manually after <c>Database.EnsureCreated()</c> because EF Core cannot manage virtual tables.
        /// <list type="bullet">
        ///   <item><term>DocumentsFts</term><description>Full-text index over Documents (Title, Content, CanonicalUrl); kept current by INSERT/UPDATE/DELETE triggers.</description></item>
        ///   <item><term>FilesFts</term><description>Index for non-text file search; rebuilt externally by <see cref="Kennedy.Data.Services.FileSearchFtsRebuilder"/>.</description></item>
        /// </list>
        /// </summary>
        public async Task EnsureFtsAsync(CancellationToken ct)
        {
            // Keep FTS schema setup explicit because EnsureCreated does not create virtual tables.
            await Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS DocumentsFts USING fts5(
                    Title,
                    Content,
                    CanonicalUrl,
                    content='Documents',
                    content_rowid='Id'
                );
                """,
                ct);

            // Trigger set keeps the FTS index synchronized with row-level document writes.
            await Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS Documents_ai AFTER INSERT ON Documents BEGIN
                    INSERT INTO DocumentsFts(rowid, Title, Content, CanonicalUrl)
                    VALUES (new.Id, new.Title, new.Content, new.CanonicalUrl);
                END;
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS Documents_ad AFTER DELETE ON Documents BEGIN
                    INSERT INTO DocumentsFts(DocumentsFts, rowid, Title, Content, CanonicalUrl)
                    VALUES ('delete', old.Id, old.Title, old.Content, old.CanonicalUrl);
                END;
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS Documents_au AFTER UPDATE ON Documents BEGIN
                    INSERT INTO DocumentsFts(DocumentsFts, rowid, Title, Content, CanonicalUrl)
                    VALUES ('delete', old.Id, old.Title, old.Content, old.CanonicalUrl);
                    INSERT INTO DocumentsFts(rowid, Title, Content, CanonicalUrl)
                    VALUES (new.Id, new.Title, new.Content, new.CanonicalUrl);
                END;
                """,
                ct);

            await Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS FilesFts USING fts5(
                    UrlRegistryId UNINDEXED,
                    SearchText
                );
                """,
                ct);
        }
    }
}
