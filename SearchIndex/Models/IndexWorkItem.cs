using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.SearchIndex.Models;

[Flags]
public enum IndexWorkType
{
    None = 0,
    File = 1,
    Image = 2,
    Popularity = 4,
}

[Table("IndexWorkItems")]
public class IndexWorkItem
{
    [Key]
    public long UrlID { get; set; }

    public IndexWorkType WorkTypes { get; set; }
}
