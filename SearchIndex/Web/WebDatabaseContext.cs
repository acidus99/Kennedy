using Microsoft.EntityFrameworkCore;

using Kennedy.SearchIndex.Models;
using System;

namespace Kennedy.SearchIndex.Web
{
    public class WebDatabaseContext : DbContext
    {
        protected string StorageDirectory;

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentLink> Links { get; set; }
        public DbSet<Domain> Domains { get; set; }
        public DbSet<Image> Images { get; set; }

        public WebDatabaseContext(string storageDir)
        {
            StorageDirectory = storageDir;
        }

        public void EnsureExists()
            => Database.EnsureCreated();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");            
        }
    }
}
