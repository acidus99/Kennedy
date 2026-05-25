using Kennedy.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data
{
    public class KennedyDbContext : DbContext
    {
        public DbSet<UrlRecord> UrlRegistry => Set<UrlRecord>();
        public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
        public DbSet<DocumentImageRecord> DocumentImages => Set<DocumentImageRecord>();
        public DbSet<UrlLinkRecord> UrlLinks => Set<UrlLinkRecord>();

        public KennedyDbContext(DbContextOptions<KennedyDbContext> options)
            : base(options)
        {
        }

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
