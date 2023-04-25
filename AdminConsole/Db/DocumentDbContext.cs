using System;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.AdminConsole.Db
{
    //only support documents
	public class DocumentDbContext : DbContext
	{
        protected string StorageDirectory;

        public DbSet<SimpleDocument> Documents { get; set; }
        
        public DocumentDbContext(string storageDir)
        {
            StorageDirectory = storageDir;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");
        }
    }
}