using Microsoft.EntityFrameworkCore;

namespace ArchiveLoader
{
    public class SimpleDocumentIndexDbContext : DbContext
    {
        protected string StorageDirectory;

        public DbSet<SimpleDocEntry> Documents { get; set; }

        public SimpleDocumentIndexDbContext(string storageDir)
        {
            StorageDirectory = storageDir;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");            
        }
    }
}
