using System;
namespace Kennedy.Data.Models
{
	public class ImageResponse : AbstractResponse
	{
        public int Width { get; set; }

        public int Height { get; set; }

        public string ImageType { get; set; }

        public bool IsTransparent { get; set; }
    }
}

