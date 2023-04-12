using System;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.AdminConsole.Importers
{
    //only support documents
	public class ModernCrawlDbContext : DbContext
	{
        protected string StorageDirectory;

        public DbSet<SimpleDocument> Documents { get; set; }
        
        public ModernCrawlDbContext(string storageDir)
        {
            StorageDirectory = storageDir;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");
        }
    }
}