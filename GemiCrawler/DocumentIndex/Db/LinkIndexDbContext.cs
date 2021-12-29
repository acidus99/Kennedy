using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;


namespace GemiCrawler.DocumentIndex.Db
{
    public class LinkIndexDbContext : AbstractSqlLiteDbContext
    {
        public LinkIndexDbContext(string storageDir)
            : base(storageDir)
        { }

        public DbSet<StoredLinkEntry> LinkEntries { get; set; }

        protected override string DbFilename => "link-index.db";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StoredLinkEntry>()
                .HasKey(l => new { l.DBSourceDocID, l.DBTargetDocID});

            base.OnModelCreating(modelBuilder);
        }
    }
}
