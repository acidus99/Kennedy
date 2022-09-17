using Microsoft.EntityFrameworkCore;

namespace Kennedy.CrawlData.Db
{
    public class DocIndexDbContext : DbContext
    {
        protected string StorageDirectory;

        public DbSet<StoredDocEntry> DocEntries { get; set; }
        public DbSet<StoredLinkEntry> LinkEntries { get; set; }
        public DbSet<StoredDomainsEntry> DomainEntries { get; set; }
        public DbSet<StoredImageEntry> ImageEntries { get; set; }
        public DbSet<ImageSearchEntry> ImageSearchEntries { get; set; }

        public DocIndexDbContext(string storageDir)
        {
            StorageDirectory = storageDir;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StoredLinkEntry>()
                .HasKey(l => new { l.DBSourceDocID, l.DBTargetDocID });

            base.OnModelCreating(modelBuilder);
        }

    }
}
