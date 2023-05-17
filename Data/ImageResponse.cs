using System;
using Gemini.Net;

namespace Kennedy.Data
{
	public class ImageResponse : ParsedResponse
	{
        public required int Width { get; init; }

        public required int Height { get; init; }

        public required string ImageType { get; init; }

        public required bool IsTransparent { get; init; }

        public ImageResponse(GeminiResponse resp)
            : base(resp)
        {
            ContentType = ContentType.Image;
        }
    }
}

