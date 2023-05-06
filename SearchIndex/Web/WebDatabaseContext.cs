using Microsoft.EntityFrameworkCore;

using Kennedy.SearchIndex.Models;
using System;

namespace Kennedy.SearchIndex.Web
{
    public class WebDatabaseContext : DbContext
    {
        protected string StorageDirectory;

        //Main entities
        public DbSet<Document> Documents { get; set; }
        public DbSet<Image> Images { get; set; }

        //aux entitites
        public DbSet<Favicon> Favicons { get; set; }
        public DbSet<RobotsTxt> RobotsTxts { get; set; }
        public DbSet<SecurityTxt> SecurityTxts { get; set; }

        public DbSet<DocumentLink> Links { get; set; }
        
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
