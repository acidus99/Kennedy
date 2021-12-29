using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;


namespace GemiCrawler.DocumentIndex.Db
{
    public class DocIndexDbContext : AbstractSqlLiteDbContext
    {

        public DocIndexDbContext(string storageDir)
            :base(storageDir)
        { }

        public DbSet<StoredDocEntry> DocEntries { get; set; }

        protected override string DbFilename => "doc-index.db";
    }
}
