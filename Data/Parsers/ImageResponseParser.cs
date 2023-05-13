using System;

using ImageMagick;

using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Data.Parsers
{
    public class ImageResponseParser : AbstractResponseParser
    {
        public override bool CanParse(GeminiResponse resp)
         => resp.HasBody && resp.IsSuccess && resp.MimeType.StartsWith("image/");

        public override ParsedResponse Parse(GeminiResponse resp)
        {
            using (var image = LoadImage(resp))
            {
                if (image != null)
                {
                    return new ImageResponse(resp)
                    {
                        ContentType = ContentType.Image,
                        IsTransparent = !image.IsOpaque,
                        Height = image.Height,
                        Width = image.Width,
                        ImageType = image.Format.ToString()
                    };
                }
            }
            return null;
        }

        public MagickImage LoadImage(GeminiResponse resp)
        {
            try
            {
                return new MagickImage(resp.BodyBytes);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

