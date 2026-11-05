using Gemini.Net;

namespace Kennedy.Data;

/// <summary>
/// Parsed result for a response whose body is a recognized image format.
/// Produced by <see cref="Parsers.BinaryParser"/> using MetadataExtractor to read dimensions and transparency metadata.
/// </summary>
public class ImageResponse : ParsedResponse
{
    /// <summary>Image width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Image format name (e.g. "PNG", "JPEG", "GIF").</summary>
    public required string ImageType { get; init; }

    /// <summary>True when available metadata declares an alpha channel or transparent colors.</summary>
    public required bool IsTransparent { get; init; }

    public ImageResponse(GeminiResponse resp)
        : base(resp)
    {
        FormatType = ContentType.Image;
    }
}