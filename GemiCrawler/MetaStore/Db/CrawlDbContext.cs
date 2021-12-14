using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;


namespace GemiCrawler.MetaStore.Db
{
    public class CrawlDbContext : DbContext
    {
        private string StorageDirectory;

        public CrawlDbContext(string storageDir = "")
        {
            StorageDirectory = storageDir;
            Database.EnsureCreated();
        }

        public DbSet<StoredResponse> Responses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string source = $"Data Source='{StorageDirectory}crawl.db'";
            options.UseSqlite(source);
        }

        #region Required
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
        #endregion

    }
}
