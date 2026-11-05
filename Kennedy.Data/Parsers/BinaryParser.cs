using FileSignatures;
using Gemini.Net;
using MetadataExtractor;
using MetadataExtractor.Formats.Bmp;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Gif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;

namespace Kennedy.Data.Parsers;

/// <summary>
/// Detects known binary formats using file magic bytes (via the FileSignatures library).
/// Returns an <see cref="ImageResponse"/> for recognized image formats (using MetadataExtractor),
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
        var bodyBytes = resp.BodyBytes ?? throw new ArgumentNullException(nameof(resp), "Response BodyBytes cannot be null");

        FileFormat? detectedType = null;

        try
        {
            using var stream = new MemoryStream(bodyBytes, writable: false);
            detectedType = inspector.DetermineFileFormat(stream);
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
            return ParseImage(resp, detectedType, bodyBytes);
        }

        return new ParsedResponse(resp)
        {
            FormatType = ContentType.Binary,
            DetectedMimeType = detectedType.MediaType
        };
    }

    /// <summary>
    /// Maps image-header metadata into the existing image model without decoding pixels.
    /// Falls back to a generic binary ParsedResponse if metadata cannot be read.
    /// </summary>
    private ParsedResponse ParseImage(GeminiResponse resp, FileFormat format, byte[] bodyBytes)
    {
        try
        {
            using var stream = new MemoryStream(bodyBytes, writable: false);
            var metadata = ImageMetadataReader.ReadMetadata(stream);
            foreach (var directory in metadata)
            {
                // Select the primary image header, not EXIF thumbnails or GIF frames.
                (int Width, int Height, string Type, bool Alpha)? info = directory switch
                {
                    JpegDirectory jpeg => (jpeg.GetImageWidth(), jpeg.GetImageHeight(), "JPEG", false),
                    PngDirectory png when png.ContainsTag(PngDirectory.TagImageWidth) =>
                        (png.GetInt32(PngDirectory.TagImageWidth), png.GetInt32(PngDirectory.TagImageHeight), "PNG",
                            png.GetInt32(PngDirectory.TagColorType) is 4 or 6 ||
                            metadata.OfType<PngDirectory>().Any(d => d.ContainsTag(PngDirectory.TagPaletteHasTransparency))),
                    GifHeaderDirectory gif =>
                        (gif.GetInt32(GifHeaderDirectory.TagImageWidth), gif.GetInt32(GifHeaderDirectory.TagImageHeight), "GIF",
                            metadata.OfType<GifControlDirectory>().Any(d =>
                                d.TryGetBoolean(GifControlDirectory.TagTransparentColorFlag, out var transparent) && transparent)),
                    WebPDirectory webp =>
                        (webp.GetInt32(WebPDirectory.TagImageWidth), webp.GetInt32(WebPDirectory.TagImageHeight), "Webp",
                            webp.TryGetBoolean(WebPDirectory.TagHasAlpha, out var alpha) && alpha),
                    BmpHeaderDirectory bmp =>
                        (bmp.GetInt32(BmpHeaderDirectory.TagImageWidth), Math.Abs(bmp.GetInt32(BmpHeaderDirectory.TagImageHeight)), "BMP",
                            bmp.TryGetInt64(BmpHeaderDirectory.TagAlphaMask, out var mask) && mask != 0),
                    ExifIfd0Directory tiff when format.MediaType == "image/tiff" =>
                        (tiff.GetInt32(ExifDirectoryBase.TagImageWidth), tiff.GetInt32(ExifDirectoryBase.TagImageHeight), "TIFF",
                            HasTiffAlpha(tiff)),
                    _ => null
                };
                if (info is not { Width: > 0, Height: > 0 } image) continue;
                return new ImageResponse(resp)
                {
                    DetectedMimeType = format.MediaType,
                    Width = image.Width,
                    Height = image.Height,
                    ImageType = image.Type,
                    IsTransparent = image.Alpha
                };
            }
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

    private static bool HasTiffAlpha(ExifIfd0Directory directory)
    {
        // ExtraSamples values 1 and 2 mean associated and unassociated alpha.
        // MetadataExtractor returns a scalar for one sample and an array for several.
        if (directory.TryGetInt32(ExifDirectoryBase.TagExtraSamples, out var sample))
            return sample is 1 or 2;
        if (directory.GetObject(ExifDirectoryBase.TagExtraSamples) is ushort[] samples)
            return samples.Any(value => value is 1 or 2);
        return directory.GetInt32Array(ExifDirectoryBase.TagExtraSamples)?.Any(value => value is 1 or 2) == true;
    }
}
