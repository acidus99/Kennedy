
using Microsoft.EntityFrameworkCore;

namespace Gemini.Net.CrawlDataStore.Db
{
    public class DocIndexDbContext : DbContext
    {

        protected string StorageDirectory;

        public DbSet<StoredDocEntry> DocEntries { get; set; }
        public DbSet<StoredLinkEntry> LinkEntries { get; set; }
        public DbSet<StoredDomainsEntry> DomainEntries { get; set; }

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
