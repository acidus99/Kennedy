using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;


namespace GemiCrawler.DocumentIndex.Db
{
    public class DocIndexDbContext : DbContext
    {
        private string StorageDirectory;

        public DocIndexDbContext(string storageDir = "")
        {
            StorageDirectory = storageDir;
            Database.EnsureCreated();
        }

        public DbSet<StoredDocEntry> Responses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string source = $"Data Source='{StorageDirectory}doc-index.db'";
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
