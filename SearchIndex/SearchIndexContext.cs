using Microsoft.EntityFrameworkCore;

using Kennedy.SearchIndex.Models;
using System;

namespace Kennedy.SearchIndex
{
    public class SearchIndexContext : DbContext
    {
        protected string StorageDirectory;

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentLink> Links { get; set; }
        public DbSet<Domain> Domains { get; set; }
        public DbSet<Image> Images { get; set; }

        public SearchIndexContext(string storageDir)
        {
            StorageDirectory = storageDir;
            Database.EnsureCreated();
            EnsureFullTextSearch(this);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");            
        }

        private void EnsureFullTextSearch(SearchIndexContext db)
        {
            using (var connection = db.Database.GetDbConnection())
            {
                connection.Open();
                var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT Count(*) FROM sqlite_master WHERE type='table' AND name='FTS';";
                var count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count == 0)
                {
                    cmd.CommandText = "CREATE VIRTUAL TABLE FTS using fts5(Title, Body, tokenize = 'porter');";
                    cmd.ExecuteNonQuery();
                }

                cmd.CommandText = "SELECT Count(*) FROM sqlite_master WHERE type='table' AND name='ImageSearch';";
                count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count == 0)
                {
                    cmd.CommandText = "CREATE VIRTUAL TABLE ImageSearch using fts5(Terms, tokenize = 'porter');";
                    cmd.ExecuteNonQuery();
                }

            }
        }
    }
}
