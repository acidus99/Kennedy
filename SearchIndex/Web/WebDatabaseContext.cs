using Kennedy.SearchIndex.Models;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Web;

public class WebDatabaseContext : DbContext
{
    protected string StorageDirectory;

    //Main entities
    public DbSet<Document> Documents { get; set; }
    public DbSet<Image> Images { get; set; }

    //aux entitites
    public DbSet<Favicon> Favicons { get; set; }
    public DbSet<RobotsTxt> RobotsTxts { get; set; }
    public DbSet<SecurityTxt> SecurityTxts { get; set; }

    public DbSet<DocumentLink> Links { get; set; }
    public DbSet<IndexWorkItem> IndexWorkItems { get; set; }

    public DbSet<FullTextSearchResult> FtsResults { get; set; }

    public DbSet<ImageSearchResult> ImageResults { get; set; }

    internal DbSet<IndexableFile> IndexableFiles { get; set; }

    public WebDatabaseContext(string storageDir)
    {
        StorageDirectory = storageDir;
    }

    public void EnsureExists()
    {
        Database.EnsureCreated();

        // EnsureCreated does not evolve an existing SQLite database. The indexer
        // must also be able to add this table to an already-populated index.
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Favicons" (
                "Protocol" TEXT NOT NULL,
                "Domain" TEXT NOT NULL,
                "Port" INTEGER NOT NULL,
                "Emoji" TEXT NOT NULL,
                "SourceUrlID" INTEGER NOT NULL,
                CONSTRAINT "PK_Favicons" PRIMARY KEY ("Domain", "Port", "Protocol")
            );
            """);
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "IndexWorkItems" (
                "UrlID" INTEGER NOT NULL CONSTRAINT "PK_IndexWorkItems" PRIMARY KEY,
                "WorkTypes" INTEGER NOT NULL
            );
            """);
        Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_Links_TargetUrlID_IsExternal"
            ON "Links" ("TargetUrlID", "IsExternal");
            """);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source='{StorageDirectory}doc-index.db'")
        //.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
        //.EnableSensitiveDataLogging(true)
        ;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Image>()
            .HasOne(i => i.Document)
            .WithOne(d => d.Image)
            .HasForeignKey<Image>(i => i.UrlID);
    }
}
