using System;

using Gemini.Net;
using Kennedy.Data.Models;

namespace Kennedy.Data.Parsers
{
	public abstract class AbstractResponseParser
	{
		public abstract bool CanParse(GeminiResponse resp);

		public abstract AbstractResponse Parse(GeminiResponse resp);
	}
}

