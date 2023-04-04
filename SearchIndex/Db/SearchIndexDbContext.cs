using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Db
{
    public class SearchIndexDbContext : DbContext
    {
        protected string StorageDirectory;

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentLink> Links { get; set; }
        public DbSet<StoredDomainsEntry> DomainEntries { get; set; }
        public DbSet<StoredImageEntry> ImageEntries { get; set; }

        public SearchIndexDbContext(string storageDir)
        {
            StorageDirectory = storageDir;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DocumentLink>()
                .HasKey(l => new { l.SourceUrlID, l.TargetUrlID });

            base.OnModelCreating(modelBuilder);
        }

    }
}
