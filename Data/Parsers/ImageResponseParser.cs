using System;

using SixLabors.ImageSharp;

using Gemini.Net;
using Kennedy.Data;
using System.IO;

namespace Kennedy.Data.Parsers
{
    public class ImageResponseParser : AbstractResponseParser
    {
        public override bool CanParse(GeminiResponse resp)
            // If we are successful, there mus be a MIME type, since the specification defines one if missing.
            => resp.HasBody && resp.IsSuccess && resp.MimeType!.StartsWith("image/");

        public override ParsedResponse? Parse(GeminiResponse resp)
        {
            if(resp.BodyBytes != null)
            {
                try
                {
                    var imageInfo = Image.Identify(resp.BodyBytes);
                    var alphaInfo = imageInfo.PixelType.AlphaRepresentation;

                    bool isTranparent = (alphaInfo != null && alphaInfo != PixelAlphaRepresentation.None);

                    return new ImageResponse(resp)
                    {
                        Height = imageInfo.Height,
                        Width = imageInfo.Width,
                        ImageType = imageInfo.Metadata.DecodedImageFormat!.Name,
                        IsTransparent = isTranparent
                    };

                } catch(Exception)
                {

                }
            }
            return null;
        }
    }
}

