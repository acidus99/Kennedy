using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.Data.Models;

/// <summary>
/// Stores decoded image metadata for a URL whose response body is an image.
/// One-to-one with <see cref="UrlRecord"/>; the PK is also the FK.
/// Populated by <see cref="Kennedy.Data.Parsers.BinaryParser"/> via ImageSharp.
/// </summary>
[Table("Images")]
public class DocumentImageRecord
{
    /// <summary>Shared PK/FK — matches the owning UrlRecord.Id.</summary>
    [Key]
    [ForeignKey(nameof(Url))]
    public long UrlRegistryId { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Format name as reported by ImageSharp (e.g. "Png", "Jpeg", "Gif").</summary>
    [MaxLength(64)]
    [Required]
    public string ImageType { get; set; } = "";

    /// <summary>True when the image has an alpha channel (non-None PixelAlphaRepresentation).</summary>
    public bool IsTransparent { get; set; }

    /// <summary>UTC timestamp of the WARC record that last wrote this image row. Used for out-of-order WARC guard.</summary>
    public DateTime LastIndexedUtc { get; set; }

    public UrlRecord? Url { get; set; }
}
