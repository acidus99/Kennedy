using Microsoft.EntityFrameworkCore;

namespace Kennedy.WarcConverters.Db;

/// <summary>
/// Simple class to support querying the Domains table in older crawl databases
/// </summary>
public class DomainDbContext : DbContext
{
    protected string StorageDirectory;

    public DbSet<SimpleDomain> Domains { get; set; }
    public DbSet<SimpleDocument> Documents { get; set; }

    public DomainDbContext(string storageDir)
    {
        StorageDirectory = storageDir;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'");
    }
}