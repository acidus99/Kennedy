using System;

using ImageMagick;

using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Data.Parsers
{
    public class ImageResponseParser : AbstractResponseParser
    {
        public override bool CanParse(GeminiResponse resp)
            // If we are successful, there mus be a MIME type, since the specification defines one if missing.
            => resp.HasBody && resp.IsSuccess && resp.MimeType!.StartsWith("image/");

        public override ParsedResponse? Parse(GeminiResponse resp)
        {
            using (var image = LoadImage(resp))
            {
                if (image != null)
                {
                    return new ImageResponse(resp)
                    {
                        IsTransparent = !image.IsOpaque,
                        Height = image.Height,
                        Width = image.Width,
                        ImageType = image.Format.ToString()
                    };
                }
            }
            return null;
        }

        public MagickImage? LoadImage(GeminiResponse resp)
        {
            try
            {
                if (resp.BodyBytes != null)
                {
                    return new MagickImage(resp.BodyBytes);
                }
            }
            catch (MagickException)
            {
            }
            return null;
        }
    }
}

