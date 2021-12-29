using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;


namespace GemiCrawler.DocumentIndex.Db
{
    public abstract class AbstractSqlLiteDbContext : DbContext
    {
        protected string StorageDirectory;

        protected abstract string DbFilename { get; }

        public AbstractSqlLiteDbContext(string storageDir)
        {
            StorageDirectory = storageDir;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string source = $"Data Source='{StorageDirectory}{DbFilename}'";
            options.UseSqlite(source);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
