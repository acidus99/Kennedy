using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.SearchIndex.Models;

[Table("Images")]
public class Image
{
    /// <summary>
    /// the ID we are using in the DB for the DocID. DocID is a ulong,
    /// but Sqlite3 doesn't support UInt64s, so we use a Int64 here and doing
    /// some unchecked casting with overflow to handle it
    /// </summary>
    [Key, ForeignKey("Document")]
    public required long UrlID { get; set; }

    [Required]
    public required int Width { get; set; }

    [Required]
    public required int Height { get; set; }

    [Required]
    public required string ImageType { get; set; }

    [Required]
    public required bool IsTransparent { get; set; }

    public Document? Document { get; set; }

    public Image()
    { }
}