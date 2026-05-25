using Microsoft.EntityFrameworkCore;

namespace Kennedy.Archive.Db;

/// <summary>
/// EF Core DbContext for the Kennedy archive database.
/// Contains the URL registry (<see cref="Urls"/>) and time-series snapshot records (<see cref="Snapshots"/>).
/// Unlike the main search database, this context configures itself directly (not via constructor injection)
/// to allow ad-hoc instantiation from <see cref="Archiver.GetContext"/>.
/// </summary>
public class ArchiveDbContext : DbContext
{
    protected string DatabasePath;

    /// <summary>Registry of every URL that has at least one archived response.</summary>
    public DbSet<Url> Urls { get; set; }

    /// <summary>Time-ordered archive records; multiple snapshots can exist per URL.</summary>
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