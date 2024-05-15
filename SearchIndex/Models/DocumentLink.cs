using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Models;

[Table("Links")]
[PrimaryKey(nameof(SourceUrlID), nameof(TargetUrlID))]
[Index(nameof(TargetUrlID))]
public class DocumentLink
{
    /// <summary>
    /// the ID we are using in the DB for the DocID. DocID is a ulong,
    /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
    /// some unchecked casting with overflow to handle it
    /// </summary>
    public long SourceUrlID { get; set; }
    public Document? SourceUrl { get; set; }

    public long TargetUrlID { get; set; }

    public bool IsExternal { get; set; }
    public string? LinkText { get; set; }
}