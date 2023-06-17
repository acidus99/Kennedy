using System;

using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Data.Parsers
{
	public abstract class AbstractTextParser
	{
		public abstract bool CanParse(GeminiResponse resp, bool isTextBody);

		public abstract ParsedResponse? Parse(GeminiResponse resp);
	}
}

