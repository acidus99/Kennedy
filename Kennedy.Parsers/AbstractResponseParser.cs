using System;

using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Parsers
{
	public abstract class AbstractResponseParser
	{
		public abstract bool CanParse(GeminiResponse resp);

		public abstract ParsedResponse Parse(GeminiResponse resp);
	}
}

