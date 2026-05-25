using Gemini.Net;

namespace Kennedy.Data;

/// <summary>
/// Parsed result for a response whose body is a recognized image format.
/// Produced by <see cref="Parsers.BinaryParser"/> using ImageSharp to decode dimensions and transparency.
/// </summary>
public class ImageResponse : ParsedResponse
{
    /// <summary>Image width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Format name from ImageSharp (e.g. "Png", "Jpeg", "Gif").</summary>
    public required string ImageType { get; init; }

    /// <summary>True when the image has a non-opaque alpha channel.</summary>
    public required bool IsTransparent { get; init; }

    public ImageResponse(GeminiResponse resp)
        : base(resp)
    {
        FormatType = ContentType.Image;
    }
}