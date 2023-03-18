using Microsoft.EntityFrameworkCore;

namespace Kennedy.Archive.Db
{
	public class ArchiveDbContext : DbContext
	{
        protected string DatabasePath;

        public DbSet<UrlEntry> Urls { get; set; }

        public DbSet<SnapshotEntry> Snapshots { get; set; }

        public ArchiveDbContext(string databasePath)
        {
            DatabasePath = databasePath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{DatabasePath}'");
                //.LogTo(Console.WriteLine)
                //.EnableSensitiveDataLogging(true);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.Entity<UrlEntry>()
            //    .HasMany(u => u.Snapshots)
            //    .WithOne(s => s.UrlEntry);

            base.OnModelCreating(modelBuilder);
        }
    }
}

