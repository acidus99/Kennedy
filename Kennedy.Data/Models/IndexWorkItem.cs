using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.Data.Models;

[Flags]
public enum IndexWorkType
{
    None = 0,
    FileSearch = 1
}

[Table("IndexWorkItems")]
public class IndexWorkItem
{
    [Key]
    public long UrlRegistryId { get; set; }

    public IndexWorkType WorkTypes { get; set; }
}
