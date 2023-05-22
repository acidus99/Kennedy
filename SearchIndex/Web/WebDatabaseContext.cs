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
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'")
            //.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            //.EnableSensitiveDataLogging(true)
            ;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Image>()
                .HasOne(i => i.Document)
                .WithOne(d => d.Image)
                .HasForeignKey<Image>(i => i.UrlID);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Favicon)
                .WithMany(f => f.Documents)
                .HasForeignKey(x => new { x.Protocol, x.Domain, x.Port });

            modelBuilder.Entity<Document>()
                .HasMany(e => e.Tags)
                .WithMany(e => e.Documents);
        }

    }
}
