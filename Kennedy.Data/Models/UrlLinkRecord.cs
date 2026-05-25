using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Models;

[Table("UrlLinks")]
[Index(nameof(SourceUrlId))]
[Index(nameof(TargetUrlId))]
public class UrlLinkRecord
{
    [Key]
    public long Id { get; set; }

    public long SourceUrlId { get; set; }

    public long TargetUrlId { get; set; }

    public bool IsExternal { get; set; }

    [MaxLength(512)]
    public string LinkText { get; set; } = "";
}
