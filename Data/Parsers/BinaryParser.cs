using System;

using FileSignatures;
using Gemini.Net;

namespace Kennedy.Data.Parsers
{
	/// <summary>
	/// Parses known Binary Formats
	/// </summary>
	public class BinaryParser
	{
		FileFormatInspector inspector = new FileFormatInspector();

		public ParsedResponse? Parse(GeminiResponse resp)
		{
			if(resp.BodyBytes == null)
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

			if(detectedType == null)
			{
				return null;
			}

			if(detectedType is FileSignatures.Formats.Image) 
			{
				return ParseImage(resp, detectedType);
			}

			return new ParsedResponse(resp)
			{
				FormatType = ContentType.Binary,
				DetectedMimeType = detectedType.MediaType
			};
		}

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
			} catch (Exception)
			{
			}

			//error parsing the image, so use a generic binary 
            return new ParsedResponse(resp)
            {
                FormatType = ContentType.Binary,
            };
        }
	}
}

