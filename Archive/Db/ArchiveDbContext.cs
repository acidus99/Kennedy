using Microsoft.EntityFrameworkCore;

namespace Kennedy.Archive.Db
{
	public class ArchiveDbContext : DbContext
	{
        protected string DatabasePath;

        public DbSet<Url> Urls { get; set; }

        public DbSet<Snapshot> Snapshots { get; set; }

        public ArchiveDbContext(string databasePath)
        {
            DatabasePath = databasePath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source='{DatabasePath}'")
                //.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
                //.EnableSensitiveDataLogging(true)
                ;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Url>()
                .HasMany(u => u.Snapshots)
                .WithOne(s => s.Url);

            base.OnModelCreating(modelBuilder);
        }
    }
}