using System;
using Gemini.Net;

namespace Kennedy.Data
{
	public class ImageResponse : ParsedResponse
	{
        public int Width { get; set; }

        public int Height { get; set; }

        public string ImageType { get; set; }

        public bool IsTransparent { get; set; }

        public ImageResponse(GeminiResponse resp)
            : base(resp)
        { }

    }
}

