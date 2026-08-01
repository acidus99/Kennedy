using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.Data.Models;

[Table("Favicons")]
[Index(nameof(Scheme), nameof(Host), nameof(Port), IsUnique = true)]
public class FaviconRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(16)]
    public string Scheme { get; set; } = "gemini";

    [Required]
    [MaxLength(255)]
    public string Host { get; set; } = "";

    public int Port { get; set; } = 1965;

    [Required]
    [MaxLength(64)]
    public string Emoji { get; set; } = "";

    public long SourceUrlRegistryId { get; set; }
}
