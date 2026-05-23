using Kennedy.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data
{
    public class KennedyDbContext : DbContext
    {
        public DbSet<UrlRecord> UrlRegistry => Set<UrlRecord>();

        public KennedyDbContext(DbContextOptions<KennedyDbContext> options)
            : base(options)
        {
        }
    }
}
