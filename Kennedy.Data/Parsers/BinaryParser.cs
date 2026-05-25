using FileSignatures;
using Gemini.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Kennedy.Data.Parsers;

/// <summary>
/// Detects known binary formats using file magic bytes (via the FileSignatures library).
/// Returns an <see cref="ImageResponse"/> for recognized image formats (using ImageSharp for metadata),
/// or a generic binary <see cref="ParsedResponse"/> for other detected binary types.
/// Returns null when no binary format is recognized, allowing the caller to fall through to text parsing.
/// </summary>
public class BinaryParser
{
    FileFormatInspector inspector = new FileFormatInspector();

    /// <summary>
    /// Attempts to identify the response body as a known binary format.
    /// Returns null if the body bytes do not match any known binary signature.
    /// </summary>
    public ParsedResponse? Parse(GeminiResponse resp)
    {
        if (resp.BodyBytes == null)
        {
            throw new ArgumentNullException(nameof(resp), "Response BodyBytes cannot be null");
        }

        FileFormat? detectedType = null;

        try
        {
            detectedType = inspector.DetermineFileFormat(new MemoryStream(resp.BodyBytes));
        }
        catch (Exception)
        {
        }

        if (detectedType == null)
        {
            return null;
        }

        if (detectedType is FileSignatures.Formats.Image)
        {
            return ParseImage(resp, detectedType);
        }

        return new ParsedResponse(resp)
        {
            FormatType = ContentType.Binary,
            DetectedMimeType = detectedType.MediaType
        };
    }

    /// <summary>
    /// Uses ImageSharp to decode image dimensions and alpha channel presence.
    /// Falls back to a generic binary ParsedResponse if ImageSharp cannot process the bytes.
    /// </summary>
    private ParsedResponse ParseImage(GeminiResponse resp, FileFormat format)
    {
        try
        {
            var imageInfo = Image.Identify(resp.BodyBytes);
            var alphaInfo = imageInfo.PixelType.AlphaRepresentation;
            bool isTranparent = (alphaInfo != null && alphaInfo != PixelAlphaRepresentation.None);

            return new ImageResponse(resp)
            {
                DetectedMimeType = format.MediaType,
                Height = imageInfo.Height,
                Width = imageInfo.Width,
                ImageType = imageInfo.Metadata.DecodedImageFormat!.Name,
                IsTransparent = isTranparent
            };
        }
        catch (Exception)
        {
        }

        //error parsing the image, so use a generic binary
        return new ParsedResponse(resp)
        {
            FormatType = ContentType.Binary,
        };
    }
}