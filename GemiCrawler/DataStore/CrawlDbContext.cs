using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;


namespace GemiCrawler.DataStore
{
    public class CrawlDbContext : DbContext
    {

        public CrawlDbContext()
        {
            Database.EnsureCreated();
        }

        public DbSet<StoredResponse> Responses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=crawl.db");


        #region Required
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
        #endregion

    }
}
