using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.Data.Models;

[Table("DocumentImages")]
public class DocumentImageRecord
{
    [Key]
    [ForeignKey(nameof(Document))]
    public long DocumentId { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    [MaxLength(64)]
    [Required]
    public string ImageType { get; set; } = "";

    public bool IsTransparent { get; set; }

    public DocumentRecord? Document { get; set; }
}
