using System;

using Gemini.Net;

namespace ArchiveLoader
{
	public class ResponseItem
	{
		public GeminiUrl Url;

		public int StatusCode;

		public string Meta;

		byte[] Data;
	}
}

